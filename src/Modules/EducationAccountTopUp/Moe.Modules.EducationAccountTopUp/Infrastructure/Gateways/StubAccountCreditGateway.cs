using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moe.Modules.EducationAccountTopUp.Domain.EducationAccounts;
using Moe.Modules.EducationAccountTopUp.IGateway;
using Moe.SharedKernel.Results;
using Moe.StudentFinance.Persistence;

namespace Moe.Modules.EducationAccountTopUp.Infrastructure.Gateways;

internal sealed class StubAccountCreditGateway(
    MoeDbContext dbContext,
    ILogger<StubAccountCreditGateway> logger) : IAccountCreditGateway
{
    private readonly ConcurrentDictionary<string, long> _processedKeys = new();

    public async Task<Result<CreditAccountResult>> CreditAccountForTopUpAsync(
        long educationAccountId,
        decimal amount,
        string idempotencyKey,
        string reason,
        CancellationToken cancellationToken = default)
    {
        if (_processedKeys.TryGetValue(idempotencyKey, out long existingTransactionId))
        {
            logger.LogInformation(
                "Stub account credit replay for idempotency key {IdempotencyKey}; AccountTransactionId={AccountTransactionId}",
                idempotencyKey,
                existingTransactionId);

            return Result<CreditAccountResult>.Success(new CreditAccountResult
            {
                AccountTransactionId = existingTransactionId,
                AlreadyProcessed = true
            });
        }

        var existingTransaction = await dbContext.Set<AccountTransaction>()
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.IdempotencyKey == idempotencyKey, cancellationToken);

        if (existingTransaction is not null)
        {
            _processedKeys.TryAdd(idempotencyKey, existingTransaction.Id);

            return Result<CreditAccountResult>.Success(new CreditAccountResult
            {
                AccountTransactionId = existingTransaction.Id,
                AlreadyProcessed = true
            });
        }

        var account = await dbContext.Set<EducationAccount>()
            .SingleOrDefaultAsync(x => x.Id == educationAccountId, cancellationToken);

        if (account is null || account.StatusCode != AccountStatuses.Active)
        {
            return Result<CreditAccountResult>.Failure(new Error(
                "Account.NotEligible",
                "Education account is not active or does not exist."));
        }

        var nowUtc = DateTime.UtcNow;
        var accountTransaction = AccountTransaction.Create(
            educationAccountId: educationAccountId,
            transactionTypeCode: "CREDIT",
            amount: amount,
            referenceTypeCode: "TOPUP",
            referenceId: TryGetTopUpRunId(idempotencyKey),
            idempotencyKey: idempotencyKey,
            currentBalance: account.CachedBalance,
            description: reason,
            createdByUserId: null,
            nowUtc: nowUtc);

        dbContext.Set<AccountTransaction>().Add(accountTransaction);
        account.UpdateBalance(amount);
        await dbContext.SaveChangesAsync(cancellationToken);

        _processedKeys.TryAdd(idempotencyKey, accountTransaction.Id);

        logger.LogInformation(
            "Stub account credit accepted for account {EducationAccountId}, amount {Amount}, idempotency key {IdempotencyKey}; AccountTransactionId={AccountTransactionId}",
            educationAccountId,
            amount,
            idempotencyKey,
            accountTransaction.Id);

        return Result<CreditAccountResult>.Success(new CreditAccountResult
        {
            AccountTransactionId = accountTransaction.Id,
            AlreadyProcessed = false
        });
    }

    private static long? TryGetTopUpRunId(string idempotencyKey)
    {
        string[] parts = idempotencyKey.Split(':');
        return parts.Length >= 2 && long.TryParse(parts[1], out long runId)
            ? runId
            : null;
    }
}
