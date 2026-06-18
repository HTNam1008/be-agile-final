using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Moe.Modules.EducationAccountTopUp.IGateway;
using Moe.SharedKernel.Results;

namespace Moe.Modules.EducationAccountTopUp.Infrastructure.Gateways;

internal sealed class StubAccountCreditGateway(
    ILogger<StubAccountCreditGateway> logger) : IAccountCreditGateway
{
    private long _nextTransactionId = 1000;
    private readonly ConcurrentDictionary<string, long> _processedKeys = new();

    public Task<Result<CreditAccountResult>> CreditAccountForTopUpAsync(
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

            return Task.FromResult(Result<CreditAccountResult>.Success(new CreditAccountResult
            {
                AccountTransactionId = existingTransactionId,
                AlreadyProcessed = true
            }));
        }

        long accountTransactionId = Interlocked.Increment(ref _nextTransactionId);
        _processedKeys.TryAdd(idempotencyKey, accountTransactionId);

        logger.LogInformation(
            "Stub account credit accepted for account {EducationAccountId}, amount {Amount}, idempotency key {IdempotencyKey}; AccountTransactionId={AccountTransactionId}",
            educationAccountId,
            amount,
            idempotencyKey,
            accountTransactionId);

        return Task.FromResult(Result<CreditAccountResult>.Success(new CreditAccountResult
        {
            AccountTransactionId = accountTransactionId,
            AlreadyProcessed = false
        }));
    }
}
