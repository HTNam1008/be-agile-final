using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moe.Modules.EducationAccountTopUp.Application.Interest;
using Moe.Modules.EducationAccountTopUp.Domain.EducationAccounts;
using Moe.Modules.IdentityPlatform.IGateway.Students;
using Moe.Modules.Notifications.Domain.Notifications;
using Moe.Modules.Notifications.IGateway.Notifications;
using Moe.SharedKernel.Results;
using Moe.StudentFinance.Persistence;

namespace Moe.Modules.EducationAccountTopUp.Infrastructure.EducationAccounts;

internal sealed class AnnualInterestProcessor(
    MoeDbContext dbContext,
    IOptions<EducationAccountInterestOptions> options,
    ILogger<AnnualInterestProcessor> logger,
    IStudentNotificationRecipientResolver notificationRecipients,
    INotificationWriter notificationWriter) : IAnnualInterestProcessor
{
    public async Task<AnnualInterestProcessingResult> ProcessDueInterestAsync(
        DateOnly todayInSingapore,
        DateTimeOffset processedAtUtc,
        CancellationToken cancellationToken = default)
    {
        EducationAccountInterestOptions currentOptions = options.Value;
        int latestInterestYear = todayInSingapore.Year - 1;
        if (latestInterestYear < currentOptions.FirstInterestYear)
        {
            return new AnnualInterestProcessingResult(latestInterestYear, 0, 0, 0m);
        }

        int processed = 0;
        int skipped = 0;
        decimal totalInterest = 0m;

        EducationAccount[] activeAccounts = await dbContext.Set<EducationAccount>()
            .Where(account => account.StatusCode == AccountStatuses.Active && account.CachedBalance > 0m)
            .OrderBy(account => account.Id)
            .ToArrayAsync(cancellationToken);

        foreach (EducationAccount account in activeAccounts)
        {
            for (int interestYear = currentOptions.FirstInterestYear; interestYear <= latestInterestYear; interestYear++)
            {
                AnnualInterestCreditResult result = await TryCreditAnnualInterestAsync(
                    account,
                    interestYear,
                    currentOptions.AnnualRate,
                    processedAtUtc,
                    cancellationToken);

                if (result.Credited)
                {
                    processed++;
                    totalInterest += result.InterestAmount;
                }
                else
                {
                    skipped++;
                }
            }
        }

        logger.LogInformation(
            "Annual education account interest processed. LatestInterestYear={InterestYear} Processed={Processed} Skipped={Skipped} TotalInterest={TotalInterest}",
            latestInterestYear,
            processed,
            skipped,
            totalInterest);

        return new AnnualInterestProcessingResult(latestInterestYear, processed, skipped, totalInterest);
    }

    private async Task<AnnualInterestCreditResult> TryCreditAnnualInterestAsync(
        EducationAccount account,
        int interestYear,
        decimal annualRate,
        DateTimeOffset processedAtUtc,
        CancellationToken cancellationToken)
    {
        DateTime yearCloseExclusiveUtc = new(interestYear + 1, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        if (account.OpenedAtUtc.UtcDateTime >= yearCloseExclusiveUtc)
        {
            return AnnualInterestCreditResult.Skipped;
        }

        string idempotencyKey = EducationAccountInterestCodes.BuildIdempotencyKey(interestYear, account.Id);
        bool alreadyCredited = await dbContext.Set<AccountTransaction>()
            .AnyAsync(transaction => transaction.IdempotencyKey == idempotencyKey, cancellationToken);

        if (alreadyCredited)
        {
            return AnnualInterestCreditResult.Skipped;
        }

        decimal balanceSnapshot = await GetClosingBalanceSnapshotAsync(
            account,
            yearCloseExclusiveUtc,
            cancellationToken);

        if (balanceSnapshot <= 0m)
        {
            return AnnualInterestCreditResult.Skipped;
        }

        decimal interestAmount = Math.Round(balanceSnapshot * annualRate, 2, MidpointRounding.AwayFromZero);
        if (interestAmount <= 0m)
        {
            return AnnualInterestCreditResult.Skipped;
        }

        AccountTransaction transaction = AccountTransaction.Create(
            educationAccountId: account.Id,
            transactionTypeCode: EducationAccountInterestCodes.TransactionTypeCode,
            amount: interestAmount,
            referenceTypeCode: EducationAccountInterestCodes.ReferenceTypeCode,
            referenceId: interestYear,
            idempotencyKey: idempotencyKey,
            currentBalance: account.CachedBalance,
            description: $"Annual 2% interest for {interestYear}",
            createdByUserId: null,
            nowUtc: processedAtUtc.UtcDateTime);

        dbContext.Set<AccountTransaction>().Add(transaction);
        account.UpdateBalance(interestAmount);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            logger.LogInformation(
                "Annual interest already credited by another process. EducationAccountId={EducationAccountId} InterestYear={InterestYear}",
                account.Id,
                interestYear);
            dbContext.Entry(transaction).State = EntityState.Detached;
            await dbContext.Entry(account).ReloadAsync(cancellationToken);
            return AnnualInterestCreditResult.Skipped;
        }

        await NotifyInterestCreditedAsync(
            account,
            interestYear,
            interestAmount,
            cancellationToken);

        return new AnnualInterestCreditResult(true, interestAmount);
    }

    private async Task NotifyInterestCreditedAsync(
        EducationAccount account,
        int interestYear,
        decimal interestAmount,
        CancellationToken cancellationToken)
    {
        try
        {
            long? userAccountId = await notificationRecipients.FindUserAccountIdByPersonIdAsync(
                account.PersonId,
                cancellationToken);

            if (userAccountId is null)
            {
                logger.LogWarning(
                    "Skipping ACC_INTEREST_CREDITED notification for education account {EducationAccountId}; no user account found for person {PersonId}",
                    account.Id,
                    account.PersonId);
                return;
            }

            string amountText = interestAmount.ToString("0.00", CultureInfo.InvariantCulture);
            await notificationWriter.CreateForBusinessFlowAsync(
                new NotificationCreateRequest(
                    userAccountId.Value,
                    NotificationTypeCode.AccInterestCredited,
                    "Annual Interest Credited",
                    $"Amount SGD {amountText} has been credited to account {account.AccountNumber} for {interestYear}."),
                logger,
                "Annual education account interest credited",
                cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(
                exception,
                "Failed to create ACC_INTEREST_CREDITED notification for education account {EducationAccountId}",
                account.Id);
        }
    }

    private async Task<decimal> GetClosingBalanceSnapshotAsync(
        EducationAccount account,
        DateTime yearCloseExclusiveUtc,
        CancellationToken cancellationToken)
    {
        decimal? ledgerBalance = await dbContext.Set<AccountTransaction>()
            .AsNoTracking()
            .Where(transaction =>
                transaction.EducationAccountId == account.Id &&
                transaction.TransactionAtUtc < yearCloseExclusiveUtc)
            .OrderByDescending(transaction => transaction.TransactionAtUtc)
            .ThenByDescending(transaction => transaction.Id)
            .Select(transaction => (decimal?)transaction.BalanceAfter)
            .FirstOrDefaultAsync(cancellationToken);

        return ledgerBalance ?? account.CachedBalance;
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException exception)
    {
        string message = exception.InnerException?.Message ?? exception.Message;
        return message.Contains("unique", StringComparison.OrdinalIgnoreCase)
            || message.Contains("duplicate", StringComparison.OrdinalIgnoreCase)
            || message.Contains("IX_AccountTransaction_IdempotencyKey", StringComparison.OrdinalIgnoreCase);
    }

    private sealed record AnnualInterestCreditResult(bool Credited, decimal InterestAmount)
    {
        public static readonly AnnualInterestCreditResult Skipped = new(false, 0m);
    }
}
