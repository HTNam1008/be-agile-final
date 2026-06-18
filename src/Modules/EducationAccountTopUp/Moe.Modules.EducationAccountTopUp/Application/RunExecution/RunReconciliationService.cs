using Microsoft.Extensions.Logging;
using Moe.Application.Abstractions.Clock;
using Moe.Application.Abstractions.Persistence;
using Moe.Modules.EducationAccountTopUp.Domain.TopUps;
using Moe.Modules.EducationAccountTopUp.IGateway.Repositories;
using Moe.SharedKernel.Results;

namespace Moe.Modules.EducationAccountTopUp.Application.RunExecution;

public sealed class RunReconciliationService(
    ITopUpRunRepository runs,
    ITopUpTransactionRepository transactions,
    IUnitOfWork unitOfWork,
    IClock clock,
    ILogger<RunReconciliationService> logger) : IRunReconciliationService
{
    public async Task<Result<ReconciliationResult>> ReconcileRunAsync(
        long topUpRunId,
        CancellationToken cancellationToken = default)
    {
        TopUpRun? run = await runs.GetByIdAsync(topUpRunId, cancellationToken);
        if (run is null)
        {
            return Result<ReconciliationResult>.Failure(TopUpErrors.RunNotFound);
        }

        List<TopUpTransaction> runTransactions = await transactions.GetByRunIdAsync(
            topUpRunId,
            cancellationToken);

        TransactionSummary summary = CalculateSummary(runTransactions);

        if (summary.TotalPending > 0)
        {
            logger.LogWarning(
                "Top-up run {TopUpRunId} has {PendingCount} pending transactions and cannot be finalized yet",
                topUpRunId,
                summary.TotalPending);

            return Result<ReconciliationResult>.Success(
                ReconciliationResult.Incomplete(topUpRunId, summary));
        }

        if (run.RunStatusCode == TopUpRunStatusCodes.Processing)
        {
            Result finalize = run.Finalize(
                summary.TotalProcessed,
                summary.TotalSucceeded,
                summary.TotalFailed,
                summary.TotalSkipped,
                summary.TotalAmount,
                clock.UtcNow.UtcDateTime);

            if (finalize.IsFailure)
            {
                return Result<ReconciliationResult>.Failure(finalize.Error);
            }

            await unitOfWork.SaveChangesAsync(cancellationToken);

            return Result<ReconciliationResult>.Success(
                ReconciliationResult.Finalized(topUpRunId, run.RunStatusCode, summary));
        }

        string? mismatch = DetectMismatch(run, summary);
        if (mismatch is not null)
        {
            logger.LogWarning(
                "Top-up run {TopUpRunId} totals mismatch detected: {Mismatch}",
                topUpRunId,
                mismatch);

            return Result<ReconciliationResult>.Success(
                ReconciliationResult.Mismatch(topUpRunId, run.RunStatusCode, summary, mismatch));
        }

        return Result<ReconciliationResult>.Success(
            ReconciliationResult.Verified(topUpRunId, run.RunStatusCode, summary));
    }

    private static TransactionSummary CalculateSummary(List<TopUpTransaction> transactions)
    {
        int completed = 0;
        int failed = 0;
        int skipped = 0;
        int pending = 0;
        decimal totalAmount = 0m;

        foreach (TopUpTransaction transaction in transactions)
        {
            switch (transaction.TransactionStatusCode)
            {
                case TopUpTransactionStatusCodes.Completed:
                    completed++;
                    totalAmount += transaction.Amount;
                    break;
                case TopUpTransactionStatusCodes.Failed:
                    failed++;
                    break;
                case TopUpTransactionStatusCodes.Skipped:
                    skipped++;
                    break;
                case TopUpTransactionStatusCodes.Pending:
                    pending++;
                    break;
            }
        }

        return new TransactionSummary
        {
            TotalSelected = transactions.Count,
            TotalProcessed = completed + failed + skipped,
            TotalSucceeded = completed,
            TotalFailed = failed,
            TotalSkipped = skipped,
            TotalPending = pending,
            TotalAmount = totalAmount
        };
    }

    private static string? DetectMismatch(TopUpRun run, TransactionSummary summary)
    {
        List<string> mismatches = [];

        if (run.TotalProcessed != summary.TotalProcessed)
        {
            mismatches.Add($"TotalProcessed: run={run.TotalProcessed}, actual={summary.TotalProcessed}");
        }

        if (run.TotalSucceeded != summary.TotalSucceeded)
        {
            mismatches.Add($"TotalSucceeded: run={run.TotalSucceeded}, actual={summary.TotalSucceeded}");
        }

        if (run.TotalFailed != summary.TotalFailed)
        {
            mismatches.Add($"TotalFailed: run={run.TotalFailed}, actual={summary.TotalFailed}");
        }

        if (run.TotalSkipped != summary.TotalSkipped)
        {
            mismatches.Add($"TotalSkipped: run={run.TotalSkipped}, actual={summary.TotalSkipped}");
        }

        if (run.TotalAmount != summary.TotalAmount)
        {
            mismatches.Add($"TotalAmount: run={run.TotalAmount}, actual={summary.TotalAmount}");
        }

        return mismatches.Count == 0 ? null : string.Join("; ", mismatches);
    }
}
