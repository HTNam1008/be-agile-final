using Microsoft.Extensions.Logging;
using Moe.Application.Abstractions.Clock;
using Moe.Application.Abstractions.Persistence;
using Moe.Modules.EducationAccountTopUp.Domain.EducationAccounts;
using Moe.Modules.EducationAccountTopUp.Domain.TopUps;
using Moe.Modules.EducationAccountTopUp.IGateway;
using Moe.Modules.EducationAccountTopUp.IGateway.Repositories;
using Moe.Modules.IdentityPlatform.IGateway.Students;
using Moe.Modules.Notifications.Domain.Notifications;
using Moe.Modules.Notifications.IGateway.Notifications;
using Moe.SharedKernel.Results;

namespace Moe.Modules.EducationAccountTopUp.Application.RunExecution;

public sealed class RunExecutionOrchestrator(
    IRecipientProcessingService recipientProcessor,
    ITopUpCampaignRepository campaigns,
    IDynamicTopUpContractRepository contracts,
    ITopUpRunRepository runs,
    IEducationAccountRepository educationAccounts,
    ITopUpExecutionEventPublisher events,
    ITopUpExecutionMetrics metrics,
    IStudentNotificationRecipientResolver studentNotificationRecipientResolver,
    ISchoolAdminNotificationRecipientResolver schoolAdminNotificationRecipientResolver,
    INotificationWriter notificationWriter,
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
                    linkedCts.Token);

                metrics.RecordRunStarted(run.Id, run.TopUpCampaignId, recipients.Count);
            }

            int totalSucceeded = 0;
            int totalFailed = 0;
            int totalSkipped = 0;
            decimal totalAmount = 0m;
            List<long> successfulAccountIds = [];

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
                        successfulAccountIds.Add(recipient.EducationAccountId);
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

            if (linkedCts.Token.IsCancellationRequested)
            {
                Result cancelResult = run.Cancel(clock.UtcNow.UtcDateTime);
                if (cancelResult.IsFailure)
                {
                    return Result<RunExecutionResult>.Failure(cancelResult.Error);
                }
                run.ReconcileCounters(totalProcessed, totalSucceeded, totalFailed, totalSkipped, totalAmount);
            }
            else
            {
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
            }

            DateTime completedAtUtc = run.CompletedAtUtc ?? clock.UtcNow.UtcDateTime;

            await CampaignLifecycleHelper.EvaluateCampaignAfterTerminalRunAsync(
                run,
                campaigns,
                contracts,
                completedAtUtc,
                linkedCts.Token);

            await unitOfWork.SaveChangesAsync(linkedCts.Token);
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
                linkedCts.Token);

            metrics.RecordRunCompleted(
                topUpRunId,
                run.TopUpCampaignId,
                run.RunStatusCode,
                totalProcessed,
                totalSucceeded,
                totalFailed,
                totalSkipped,
                duration);

            await NotifyRunCompletedToSchoolAdminsAsync(
                run,
                recipients.Count,
                totalSucceeded,
                totalFailed,
                totalSkipped,
                totalAmount,
                linkedCts.Token);

            return Result<RunExecutionResult>.Success(new RunExecutionResult
            {
                TopUpRunId = topUpRunId,
                TerminalStatus = run.RunStatusCode,
                TotalSelected = recipients.Count,
                TotalProcessed = totalProcessed,
                TotalSucceeded = totalSucceeded,
                TotalFailed = totalFailed,
                TotalSkipped = totalSkipped,
                TotalAmount = totalAmount,
                SuccessfulAccountIds = successfulAccountIds
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

    public void RegisterCancellationToken(long topUpRunId, CancellationTokenSource cts)
    {
        _cancellationTokens.TryAdd(topUpRunId, cts);
    }

    public void UnregisterCancellationToken(long topUpRunId)
    {
        _cancellationTokens.TryRemove(topUpRunId, out _);
    }

    public async Task<Result<ChunkProcessingResult>> ProcessChunkAsync(
        long topUpRunId,
        IReadOnlyList<RecipientInfo> chunk,
        ChunkProcessingAccumulator accumulator,
        CancellationToken cancellationToken)
    {
        int chunkSucceeded = 0;
        int chunkFailed = 0;
        int chunkSkipped = 0;
        decimal chunkAmount = 0m;
        List<long> chunkSuccessfulAccountIds = [];

        CancellationTokenSource? cts = null;
        if (_cancellationTokens.TryGetValue(topUpRunId, out var existingCts))
        {
            cts = existingCts;
        }

        foreach (RecipientInfo recipient in chunk)
        {
            if (cts?.Token.IsCancellationRequested == true)
            {
                chunkSkipped++;
                accumulator.TotalSkipped++;
                continue;
            }

            RecipientProcessingResult result = await ProcessRecipientWithRetryAsync(
                topUpRunId,
                recipient,
                cts?.Token ?? cancellationToken);

            switch (result.Status)
            {
                case TopUpTransactionStatusCodes.Completed:
                    chunkSucceeded++;
                    chunkAmount += result.CreditedAmount;
                    chunkSuccessfulAccountIds.Add(recipient.EducationAccountId);
                    accumulator.TotalSucceeded++;
                    accumulator.TotalAmount += result.CreditedAmount;
                    accumulator.SuccessfulAccountIds.Add(recipient.EducationAccountId);
                    break;
                case TopUpTransactionStatusCodes.Failed:
                    chunkFailed++;
                    accumulator.TotalFailed++;
                    break;
                case TopUpTransactionStatusCodes.Skipped:
                    chunkSkipped++;
                    accumulator.TotalSkipped++;
                    break;
            }
        }

        return Result<ChunkProcessingResult>.Success(new ChunkProcessingResult(
            chunkSucceeded, chunkFailed, chunkSkipped, chunkAmount, chunkSuccessfulAccountIds));
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

    private async Task CreateTopUpReceivedNotificationAsync(
        long topUpRunId,
        RecipientInfo recipient,
        CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "CreateTopUpReceivedNotificationAsync started for run {TopUpRunId}, education account {EducationAccountId}",
            topUpRunId,
            recipient.EducationAccountId);

        EducationAccount? account = await educationAccounts.FindByIdAsync(
            recipient.EducationAccountId,
            cancellationToken);

        if (account is null)
        {
            logger.LogWarning(
                "Skipping top-up notification for education account {EducationAccountId}; account not found",
                recipient.EducationAccountId);
            return;
        }

        long? userAccountId = await studentNotificationRecipientResolver.FindUserAccountIdByPersonIdAsync(
            account.PersonId,
            cancellationToken);

        if (userAccountId is null)
        {
            logger.LogWarning(
                "Skipping top-up notification for education account {EducationAccountId}; no user account found for person {PersonId}",
                recipient.EducationAccountId,
                account.PersonId);
            return;
        }

        string amount = recipient.Amount.ToString("0.00");
        string accountNumber = account.AccountNumber;

        logger.LogInformation(
            "Creating TOP_UP_RECEIVED notification for user account {UserAccountId} from education account {EducationAccountId} in run {TopUpRunId}",
            userAccountId.Value,
            recipient.EducationAccountId,
            topUpRunId);

        bool created = await notificationWriter.CreateForBusinessFlowAsync(
            new NotificationCreateRequest(
                userAccountId.Value,
                NotificationTypeCode.TopUpReceived,
                $"Top-up Credited: {accountNumber}",
                $"Amount {amount} has been credited to account {accountNumber}."),
            logger,
            "Top-up run recipient credited",
            cancellationToken);

        if (created)
        {
            logger.LogInformation(
                "TOP_UP_RECEIVED notification created successfully for user account {UserAccountId} in run {TopUpRunId}",
                userAccountId.Value,
                topUpRunId);
        }
    }

    private async Task NotifyRunCompletedToSchoolAdminsAsync(
        TopUpRun run,
        int totalSelected,
        int totalSucceeded,
        int totalFailed,
        int totalSkipped,
        decimal totalAmount,
        CancellationToken cancellationToken)
    {
        if (totalSelected <= 0 || totalSucceeded != totalSelected || totalFailed > 0 || totalSkipped > 0)
        {
            logger.LogInformation(
                "Skipping RUN_COMPLETED notification for run {TopUpRunId}; completion criteria not met (selected={TotalSelected}, succeeded={TotalSucceeded}, failed={TotalFailed}, skipped={TotalSkipped})",
                run.Id,
                totalSelected,
                totalSucceeded,
                totalFailed,
                totalSkipped);
            return;
        }

        TopUpCampaign? campaign = await campaigns.GetByIdAsync(run.TopUpCampaignId, cancellationToken);
        if (campaign is null)
        {
            logger.LogWarning(
                "Skipping RUN_COMPLETED notification for run {TopUpRunId}; campaign {TopUpCampaignId} not found",
                run.Id,
                run.TopUpCampaignId);
            return;
        }

        IReadOnlyCollection<long> schoolAdminUserAccountIds = await schoolAdminNotificationRecipientResolver.FindUserAccountIdsByOrganizationIdAsync(
            campaign.OrganizationId,
            cancellationToken);

        if (schoolAdminUserAccountIds.Count == 0)
        {
            logger.LogWarning(
                "Skipping RUN_COMPLETED notification for run {TopUpRunId}; no school admin user accounts found for organization {OrganizationId}",
                run.Id,
                campaign.OrganizationId);
            return;
        }

        logger.LogInformation(
            "Creating RUN_COMPLETED notification for run {TopUpRunId} for {AdminCount} school admin(s) in organization {OrganizationId}",
            run.Id,
            schoolAdminUserAccountIds.Count,
            campaign.OrganizationId);

        foreach (long userAccountId in schoolAdminUserAccountIds)
        {
            bool created = await notificationWriter.CreateForBusinessFlowAsync(
                new NotificationCreateRequest(
                    userAccountId,
                    NotificationTypeCode.RunCompleted,
                    $"Top-up Run {run.Id} Completed",
                    $"Run {run.Id} completed with {totalSucceeded} successful student(s), {totalFailed} failed, {totalSkipped} skipped, and total amount {totalAmount:0.00}."),
                logger,
                "Top-up run completed school admin notification",
                cancellationToken);

            if (created)
            {
                logger.LogInformation(
                    "RUN_COMPLETED notification created successfully for school admin {UserAccountId} in run {TopUpRunId}",
                    userAccountId,
                    run.Id);
            }
        }
    }
}





