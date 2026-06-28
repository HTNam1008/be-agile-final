using Microsoft.Extensions.Logging;
using Moe.Application.Abstractions.Clock;
using Moe.Application.Abstractions.Persistence;
using Moe.Modules.EducationAccountTopUp.Domain.TopUps;
using Moe.Modules.EducationAccountTopUp.IGateway;
using Moe.Modules.EducationAccountTopUp.IGateway.Repositories;
using Moe.SharedKernel.Results;

namespace Moe.Modules.EducationAccountTopUp.Application.RunExecution;

public sealed class PendingTransactionRecoveryService(
    ITopUpTransactionRepository transactions,
    ITopUpRunRepository runs,
    IAccountCreditGateway accountCreditGateway,
    ITopUpExecutionEventPublisher events,
    ITopUpExecutionMetrics metrics,
    IUnitOfWork unitOfWork,
    IClock clock,
    ILogger<PendingTransactionRecoveryService> logger) : IPendingTransactionRecoveryService
{
    public async Task<int> RecoverPendingTransactionsAsync(
        long topUpRunId,
        string campaignReason,
        CancellationToken cancellationToken = default)
    {
        List<TopUpTransaction> runTransactions = await transactions.GetByRunIdAsync(
            topUpRunId,
            cancellationToken);

        List<TopUpTransaction> pendingTransactions = runTransactions
            .Where(transaction => transaction.TransactionStatusCode == TopUpTransactionStatusCodes.Pending)
            .ToList();

        if (pendingTransactions.Count == 0)
        {
            return 0;
        }

        logger.LogInformation(
            "Recovering {PendingCount} pending top-up transactions for run {TopUpRunId}",
            pendingTransactions.Count,
            topUpRunId);

        int recovered = 0;

        foreach (TopUpTransaction transaction in pendingTransactions)
        {
            try
            {
                Result<CreditAccountResult> credit = await accountCreditGateway.CreditAccountForTopUpAsync(
                    transaction.EducationAccountId,
                    transaction.Amount,
                    transaction.IdempotencyKey,
                    campaignReason,
                    cancellationToken);

                if (credit.IsSuccess)
                {
                    Result complete = transaction.Complete(
                        credit.Value.AccountTransactionId,
                        clock.UtcNow.UtcDateTime);

                    if (complete.IsSuccess)
                    {
                        DateTime completedAtUtc = clock.UtcNow.UtcDateTime;
                        await unitOfWork.SaveChangesAsync(cancellationToken);

                        await events.PublishTopUpReceivedAsync(
                            new TopUpReceivedReport
                            {
                                TopUpRunId = topUpRunId,
                                TopUpTransactionId = transaction.Id,
                                EducationAccountId = transaction.EducationAccountId,
                                AccountTransactionId = credit.Value.AccountTransactionId,
                                Amount = transaction.Amount,
                                AlreadyProcessed = credit.Value.AlreadyProcessed,
                                OccurredAtUtc = completedAtUtc
                            },
                            cancellationToken);

                        metrics.RecordRecipientProcessed(
                            topUpRunId,
                            TopUpTransactionStatusCodes.Completed,
                            credit.Value.AlreadyProcessed,
                            accountCreditFailure: false);

                        recovered++;

                        logger.LogInformation(
                            "Recovered pending top-up transaction {TopUpTransactionId} for run {TopUpRunId}; alreadyProcessed={AlreadyProcessed}",
                            transaction.Id,
                            topUpRunId,
                            credit.Value.AlreadyProcessed);
                    }

                    continue;
                }

                Result fail = transaction.Fail(
                    credit.Error.Message,
                    clock.UtcNow.UtcDateTime);

                if (fail.IsSuccess)
                {
                    await unitOfWork.SaveChangesAsync(cancellationToken);

                    metrics.RecordRecipientProcessed(
                        topUpRunId,
                        TopUpTransactionStatusCodes.Failed,
                        duplicateIdempotencyHit: false,
                        accountCreditFailure: true);
                }
            }
            catch (Exception exception)
            {
                metrics.RecordRecipientProcessed(
                    topUpRunId,
                    TopUpTransactionStatusCodes.Failed,
                    duplicateIdempotencyHit: false,
                    accountCreditFailure: true);

                logger.LogError(
                    exception,
                    "Failed to recover pending top-up transaction {TopUpTransactionId} for run {TopUpRunId}",
                    transaction.Id,
                    topUpRunId);
            }
        }

        if (recovered > 0)
        {
            TopUpRun? run = await runs.GetByIdAsync(topUpRunId, cancellationToken);
            if (run is not null)
            {
                List<TopUpTransaction> allTransactions = await transactions.GetByRunIdAsync(
                    topUpRunId,
                    cancellationToken);

                int totalProcessed = allTransactions.Count;
                int totalSucceeded = allTransactions.Count(t => t.TransactionStatusCode == TopUpTransactionStatusCodes.Completed);
                int totalFailed = allTransactions.Count(t => t.TransactionStatusCode == TopUpTransactionStatusCodes.Failed);
                int totalSkipped = allTransactions.Count(t => t.TransactionStatusCode == TopUpTransactionStatusCodes.Skipped);
                decimal totalAmount = allTransactions
                    .Where(t => t.TransactionStatusCode == TopUpTransactionStatusCodes.Completed)
                    .Sum(t => t.Amount);

                run.ReconcileCounters(totalProcessed, totalSucceeded, totalFailed, totalSkipped, totalAmount);
                await unitOfWork.SaveChangesAsync(cancellationToken);

                logger.LogInformation(
                    "Reconciled run {TopUpRunId} counters: {TotalProcessed} processed, {TotalSucceeded} succeeded, {TotalFailed} failed, {TotalSkipped} skipped, {TotalAmount} total",
                    topUpRunId,
                    totalProcessed,
                    totalSucceeded,
                    totalFailed,
                    totalSkipped,
                    totalAmount);
            }
        }

        return recovered;
    }
}
