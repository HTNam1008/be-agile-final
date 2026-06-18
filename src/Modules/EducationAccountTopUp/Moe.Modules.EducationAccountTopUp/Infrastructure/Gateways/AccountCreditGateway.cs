using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moe.Application.Abstractions.Clock;
using Moe.Modules.EducationAccountTopUp.Domain.EducationAccounts;
using Moe.Modules.EducationAccountTopUp.Domain.TopUps;
using Moe.Modules.EducationAccountTopUp.IGateway;
using Moe.SharedKernel.Results;
using Moe.StudentFinance.Persistence;

namespace Moe.Modules.EducationAccountTopUp.Infrastructure.Gateways;

/// <summary>
/// Temporarily hosts ledger posting for top-up credits until the Account module exposes its service.
/// TODO: Replace this adapter with a cross-module Account credit service when it is available.
/// </summary>
internal sealed class AccountCreditGateway(
    MoeDbContext dbContext,
    IClock clock,
    ITopUpExecutionMetrics metrics,
    ILogger<AccountCreditGateway> logger) : IAccountCreditGateway
{
    private const string CreditTransactionTypeCode = "CREDIT";
    private const string TopUpReferenceTypeCode = "TOPUP";

    public async Task<Result<CreditAccountResult>> CreditAccountForTopUpAsync(
        long educationAccountId,
        decimal amount,
        string idempotencyKey,
        string reason,
        CancellationToken cancellationToken = default)
    {
        Result<CreditAccountResult>? existing = await TryGetExistingCreditAsync(
            idempotencyKey,
            cancellationToken);

        if (existing is not null)
        {
            return existing;
        }

        if (amount <= 0)
        {
            return Result<CreditAccountResult>.Failure(TopUpErrors.InvalidCreditAmount);
        }

        EducationAccount? account = await dbContext.Set<EducationAccount>()
            .FirstOrDefaultAsync(x => x.Id == educationAccountId, cancellationToken);

        if (account is null)
        {
            return Result<CreditAccountResult>.Failure(TopUpErrors.AccountNotFound);
        }

        if (account.StatusCode != AccountStatuses.Active)
        {
            return Result<CreditAccountResult>.Failure(TopUpErrors.AccountNotActive);
        }

        AccountTransaction transaction = AccountTransaction.Create(
            educationAccountId: educationAccountId,
            transactionTypeCode: CreditTransactionTypeCode,
            amount: amount,
            referenceTypeCode: TopUpReferenceTypeCode,
            referenceId: null,
            idempotencyKey: idempotencyKey,
            currentBalance: account.CachedBalance,
            description: reason,
            createdByUserId: null,
            nowUtc: clock.UtcNow.UtcDateTime);

        dbContext.Set<AccountTransaction>().Add(transaction);
        account.UpdateBalance(amount);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            metrics.RecordAccountCreditDbConflict();

            Result<CreditAccountResult>? raceResult = await TryGetExistingCreditAsync(
                idempotencyKey,
                cancellationToken);

            if (raceResult is not null)
            {
                return raceResult;
            }

            throw;
        }

        logger.LogInformation(
            "Credited account {EducationAccountId} with {Amount}; AccountTransactionId={AccountTransactionId}",
            educationAccountId,
            amount,
            transaction.Id);

        return Result<CreditAccountResult>.Success(new CreditAccountResult
        {
            AccountTransactionId = transaction.Id,
            AlreadyProcessed = false
        });
    }

    private async Task<Result<CreditAccountResult>?> TryGetExistingCreditAsync(
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        AccountTransaction? existingTransaction = await dbContext.Set<AccountTransaction>()
            .FirstOrDefaultAsync(x => x.IdempotencyKey == idempotencyKey, cancellationToken);

        if (existingTransaction is null)
        {
            return null;
        }

        logger.LogInformation(
            "Credit already processed for idempotency key {IdempotencyKey}; AccountTransactionId={AccountTransactionId}",
            idempotencyKey,
            existingTransaction.Id);

        return Result<CreditAccountResult>.Success(new CreditAccountResult
        {
            AccountTransactionId = existingTransaction.Id,
            AlreadyProcessed = true
        });
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException exception)
    {
        string? message = exception.InnerException?.Message ?? exception.Message;

        return message.Contains("unique", StringComparison.OrdinalIgnoreCase)
            || message.Contains("duplicate", StringComparison.OrdinalIgnoreCase)
            || message.Contains("IX_AccountTransaction_IdempotencyKey", StringComparison.OrdinalIgnoreCase);
    }
}
