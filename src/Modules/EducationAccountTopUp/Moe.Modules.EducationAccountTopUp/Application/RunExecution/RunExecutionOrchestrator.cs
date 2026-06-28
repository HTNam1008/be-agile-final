using Microsoft.Extensions.Logging;
using Moe.Application.Abstractions.Clock;
using Moe.Application.Abstractions.Persistence;
using Moe.Modules.EducationAccountTopUp.Domain.TopUps;
using Moe.Modules.EducationAccountTopUp.IGateway;
using Moe.Modules.EducationAccountTopUp.IGateway.Repositories;
using Moe.SharedKernel.Results;

namespace Moe.Modules.EducationAccountTopUp.Application.RunExecution;

public sealed class RunExecutionOrchestrator(
    IRecipientProcessingService recipientProcessor,
    ITopUpCampaignRepository campaigns,
    ITopUpRunRepository runs,
    ITopUpTransactionRepository transactions,
    ITopUpExecutionEventPublisher events,
    ITopUpExecutionMetrics metrics,
    IUnitOfWork unitOfWork,
    IClock clock,
    ILogger<RunExecutionOrchestrator> logger) : IRunExecutionOrchestrator
{
    private const int MaxTransientRetries = 3;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(500);
    private readonly System.Collections.Concurrent.ConcurrentDictionary<long, CancellationTokenSource> _cancellationTokens = new();

    public async Task<Result<RunExecutionResult>> ExecuteRunAsync(
        long topUpRunId,
        IReadOnlyList<RecipientInfo> recipients,
        CancellationToken cancellationToken = default)
    {
        using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _cancellationTokens.TryAdd(topUpRunId, linkedCts);

        try
        {
            _ = transactions;

        TopUpRun? run = await runs.GetByIdAsync(topUpRunId, cancellationToken);
        if (run is null)
        {
            return Result<RunExecutionResult>.Failure(TopUpErrors.RunNotFound);
        }

        if (TopUpRunStatusCodes.TerminalStatuses.Contains(run.RunStatusCode))
        {
            return Result<RunExecutionResult>.Failure(TopUpErrors.RunAlreadyTerminal);
        }

        if (run.RunStatusCode == TopUpRunStatusCodes.Processing)
        {
            logger.LogInformation(
                "Top-up run {TopUpRunId} is already processing; continuing execution",
                topUpRunId);
        }
        else
        {
            Result start = run.StartProcessing(clock.UtcNow.UtcDateTime);
            if (start.IsFailure)
            {
                return Result<RunExecutionResult>.Failure(start.Error);
            }

            Result selected = run.SetTotalSelected(recipients.Count);
            if (selected.IsFailure)
            {
                return Result<RunExecutionResult>.Failure(selected.Error);
            }

            await unitOfWork.SaveChangesAsync(cancellationToken);

            await events.PublishRunStartedAsync(
                new TopUpRunStartedReport
                {
                    TopUpRunId = run.Id,
                    CampaignId = run.TopUpCampaignId,
                    TotalSelected = recipients.Count,
                    OccurredAtUtc = run.StartedAtUtc ?? clock.UtcNow.UtcDateTime
                },
                cancellationToken);

            metrics.RecordRunStarted(run.Id, run.TopUpCampaignId, recipients.Count);
        }

        int totalSucceeded = 0;
        int totalFailed = 0;
        int totalSkipped = 0;
        decimal totalAmount = 0m;

        foreach (RecipientInfo recipient in recipients)
        {
            if (linkedCts.Token.IsCancellationRequested)
            {
                totalSkipped++;
                continue;
            }

            RecipientProcessingResult result = await ProcessRecipientWithRetryAsync(
                topUpRunId,
                recipient,
                linkedCts.Token);

            switch (result.Status)
            {
                case TopUpTransactionStatusCodes.Completed:
                    totalSucceeded++;
                    totalAmount += result.CreditedAmount;
                    break;
                case TopUpTransactionStatusCodes.Failed:
                    totalFailed++;
                    break;
                case TopUpTransactionStatusCodes.Skipped:
                    totalSkipped++;
                    break;
            }
        }

        int totalProcessed = totalSucceeded + totalFailed + totalSkipped;
        Result finalize = run.Finalize(
            totalProcessed,
            totalSucceeded,
            totalFailed,
            totalSkipped,
            totalAmount,
            clock.UtcNow.UtcDateTime);

        if (finalize.IsFailure)
        {
            return Result<RunExecutionResult>.Failure(finalize.Error);
        }

        DateTime completedAtUtc = run.CompletedAtUtc ?? clock.UtcNow.UtcDateTime;

        await CampaignLifecycleHelper.EvaluateCampaignAfterTerminalRunAsync(
            run,
            campaigns,
            completedAtUtc,
            cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        TimeSpan duration = run.StartedAtUtc is DateTime startedAtUtc
            ? completedAtUtc - startedAtUtc
            : TimeSpan.Zero;

        await events.PublishRunCompletedAsync(
            new TopUpRunCompletedReport
            {
                TopUpRunId = topUpRunId,
                CampaignId = run.TopUpCampaignId,
                TerminalStatus = run.RunStatusCode,
                TotalProcessed = totalProcessed,
                TotalSucceeded = totalSucceeded,
                TotalFailed = totalFailed,
                TotalSkipped = totalSkipped,
                TotalAmount = totalAmount,
                OccurredAtUtc = completedAtUtc
            },
            cancellationToken);

        metrics.RecordRunCompleted(
            topUpRunId,
            run.TopUpCampaignId,
            run.RunStatusCode,
            totalProcessed,
            totalSucceeded,
            totalFailed,
            totalSkipped,
            duration);

        return Result<RunExecutionResult>.Success(new RunExecutionResult
        {
            TopUpRunId = topUpRunId,
            TerminalStatus = run.RunStatusCode,
            TotalSelected = recipients.Count,
            TotalProcessed = totalProcessed,
            TotalSucceeded = totalSucceeded,
            TotalFailed = totalFailed,
            TotalSkipped = totalSkipped,
            TotalAmount = totalAmount
        });
        }
        finally
        {
            _cancellationTokens.TryRemove(topUpRunId, out _);
        }
    }

    public bool CancelRun(long topUpRunId)
    {
        if (_cancellationTokens.TryGetValue(topUpRunId, out CancellationTokenSource? cts))
        {
            cts.Cancel();
            return true;
        }
        return false;
    }

    private async Task<RecipientProcessingResult> ProcessRecipientWithRetryAsync(
        long topUpRunId,
        RecipientInfo recipient,
        CancellationToken cancellationToken)
    {
        RecipientProcessingResult? lastResult = null;

        for (int attempt = 1; attempt <= MaxTransientRetries; attempt++)
        {
            try
            {
                Result<RecipientProcessingResult> result = await recipientProcessor.ProcessRecipientAsync(
                    topUpRunId,
                    recipient.EducationAccountId,
                    recipient.Amount,
                    recipient.OrganizationUnitId,
                    recipient.CampaignReason,
                    cancellationToken);

                if (result.IsSuccess)
                {
                    lastResult = result.Value;

                    if (lastResult.Status != TopUpTransactionStatusCodes.Failed)
                    {
                        return lastResult;
                    }

                    FailureKind failureKind = FailureClassifier.Classify(lastResult.Reason ?? string.Empty);
                    if (failureKind == FailureKind.Permanent)
                    {
                        return lastResult;
                    }

                    logger.LogWarning(
                        "Transient recipient failure for account {EducationAccountId} in run {TopUpRunId}, attempt {Attempt}/{MaxTransientRetries}",
                        recipient.EducationAccountId,
                        topUpRunId,
                        attempt,
                        MaxTransientRetries);
                }
                else
                {
                    FailureKind failureKind = FailureClassifier.Classify(result.Error.Code);
                    if (failureKind == FailureKind.Permanent)
                    {
                        return RecipientProcessingResult.Failed(0, result.Error.Message);
                    }

                    logger.LogWarning(
                        "Transient recipient error for account {EducationAccountId} in run {TopUpRunId}, attempt {Attempt}/{MaxTransientRetries}: {ErrorCode}",
                        recipient.EducationAccountId,
                        topUpRunId,
                        attempt,
                        MaxTransientRetries,
                        result.Error.Code);
                }
            }
            catch (Exception exception)
            {
                FailureKind failureKind = FailureClassifier.Classify(exception);
                if (failureKind == FailureKind.Permanent)
                {
                    logger.LogError(
                        exception,
                        "Permanent recipient failure for account {EducationAccountId} in run {TopUpRunId}",
                        recipient.EducationAccountId,
                        topUpRunId);

                    return RecipientProcessingResult.Failed(0, SafeReasons.UnexpectedError);
                }

                logger.LogWarning(
                    exception,
                    "Transient recipient exception for account {EducationAccountId} in run {TopUpRunId}, attempt {Attempt}/{MaxTransientRetries}",
                    recipient.EducationAccountId,
                    topUpRunId,
                    attempt,
                    MaxTransientRetries);
            }

            if (attempt < MaxTransientRetries)
            {
                await Task.Delay(RetryDelay * attempt, cancellationToken);
            }
        }

        logger.LogError(
            "Exhausted transient retries for account {EducationAccountId} in run {TopUpRunId}",
            recipient.EducationAccountId,
            topUpRunId);

        return lastResult ?? RecipientProcessingResult.Failed(0, SafeReasons.TransientErrorExhaustedRetries);
    }
}
