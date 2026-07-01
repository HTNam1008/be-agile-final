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
    ITopUpRunRepository runs,
    IEducationAccountRepository educationAccounts,
    ITopUpTransactionRepository transactions,
    ITopUpExecutionEventPublisher events,
    ITopUpExecutionMetrics metrics,
    IStudentNotificationRecipientResolver notificationRecipientResolver,
    INotificationWriter notificationWriter,
    IUnitOfWork unitOfWork,
    IClock clock,
    ILogger<RunExecutionOrchestrator> logger) : IRunExecutionOrchestrator
{
    private const int MaxTransientRetries = 3;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(500);

    public async Task<Result<RunExecutionResult>> ExecuteRunAsync(
        long topUpRunId,
        IReadOnlyList<RecipientInfo> recipients,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "ExecuteRunAsync started for run {TopUpRunId} with {RecipientCount} recipient(s)",
            topUpRunId,
            recipients.Count);

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
            cancellationToken.ThrowIfCancellationRequested();

            logger.LogInformation(
                "Processing recipient for run {TopUpRunId}: education account {EducationAccountId}, amount {Amount}, organization unit {OrganizationUnitId}",
                topUpRunId,
                recipient.EducationAccountId,
                recipient.Amount,
                recipient.OrganizationUnitId);

            RecipientProcessingResult result = await ProcessRecipientWithRetryAsync(
                topUpRunId,
                recipient,
                cancellationToken);

            switch (result.Status)
            {
                case TopUpTransactionStatusCodes.Completed:
                    totalSucceeded++;
                    totalAmount += result.CreditedAmount;
                    logger.LogInformation(
                        "Recipient completed for run {TopUpRunId}, education account {EducationAccountId}, credited amount {CreditedAmount}, already processed {AlreadyProcessed}",
                        topUpRunId,
                        recipient.EducationAccountId,
                        result.CreditedAmount,
                        result.AlreadyProcessed);

                    if (!result.AlreadyProcessed)
                    {
                        await CreateTopUpReceivedNotificationAsync(topUpRunId, recipient, cancellationToken);
                    }
                    break;
                case TopUpTransactionStatusCodes.Failed:
                    logger.LogWarning(
                        "Recipient failed for run {TopUpRunId}, education account {EducationAccountId}, reason {Reason}",
                        topUpRunId,
                        recipient.EducationAccountId,
                        result.Reason ?? "N/A");
                    totalFailed++;
                    break;
                case TopUpTransactionStatusCodes.Skipped:
                    logger.LogWarning(
                        "Recipient skipped for run {TopUpRunId}, education account {EducationAccountId}, reason {Reason}",
                        topUpRunId,
                        recipient.EducationAccountId,
                        result.Reason ?? "N/A");
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

        TopUpCampaign? campaign = await campaigns.GetByIdAsync(run.TopUpCampaignId, cancellationToken);
        if (campaign is not null)
        {
            logger.LogInformation(
                "Campaign {TopUpCampaignId} loaded after run completion. ScheduleType={ScheduleTypeCode}, NextRunAtUtc={NextRunAtUtc}",
                campaign.Id,
                campaign.ScheduleTypeCode,
                campaign.NextRunAtUtc);

            if (string.Equals(campaign.ScheduleTypeCode, "IMMEDIATE", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(campaign.ScheduleTypeCode, "ONETIME_SCHEDULED", StringComparison.OrdinalIgnoreCase))
            {
                campaign.ChangeStatus(TopUpCampaignStatusCodes.Completed, 0, completedAtUtc, true);
            }
            else if (string.Equals(campaign.ScheduleTypeCode, "RECURRING", StringComparison.OrdinalIgnoreCase) && campaign.NextRunAtUtc == null)
            {
                campaign.ChangeStatus(TopUpCampaignStatusCodes.Completed, 0, completedAtUtc, true);
            }
        }

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

        if (run.TriggeredByLoginAccountId is long triggeredByUserAccountId)
        {
            logger.LogInformation(
                "Run {TopUpRunId} triggered by user account {UserAccountId}; creating RUN_COMPLETED notification",
                run.Id,
                triggeredByUserAccountId);

            await NotifyRunCompletedAsync(
                run,
                triggeredByUserAccountId,
                totalSucceeded,
                totalAmount,
                cancellationToken);
        }

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

    private async Task<RecipientProcessingResult> ProcessRecipientWithRetryAsync(
        long topUpRunId,
        RecipientInfo recipient,
        CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "ProcessRecipientWithRetryAsync started for run {TopUpRunId}, education account {EducationAccountId}",
            topUpRunId,
            recipient.EducationAccountId);

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
                    logger.LogInformation(
                        "Recipient processor returned status {Status} for education account {EducationAccountId} in run {TopUpRunId}",
                        lastResult.Status,
                        recipient.EducationAccountId,
                        topUpRunId);

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

        long? userAccountId = await notificationRecipientResolver.FindUserAccountIdByPersonIdAsync(
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

        Result<long> create = await notificationWriter.CreateAsync(
            new NotificationCreateRequest(
                userAccountId.Value,
                NotificationTypeCode.TopUpReceived,
                "Student Support Received",
                $"Amount {amount} has been credited to account {accountNumber}."),
            cancellationToken);

        if (create.IsFailure)
        {
            logger.LogWarning(
                "Failed to create top-up notification for user account {UserAccountId} in run {TopUpRunId}: {ErrorCode}",
                userAccountId.Value,
                topUpRunId,
                create.Error.Code);
        }
        else
        {
            logger.LogInformation(
                "TOP_UP_RECEIVED notification created successfully with NotificationId {NotificationId} for user account {UserAccountId} in run {TopUpRunId}",
                create.Value,
                userAccountId.Value,
                topUpRunId);
        }
    }

    private async Task NotifyRunCompletedAsync(
        TopUpRun run,
        long userAccountId,
        int totalSucceeded,
        decimal totalAmount,
        CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "NotifyRunCompletedAsync started for run {TopUpRunId}, user account {UserAccountId}, totalSucceeded {TotalSucceeded}, totalAmount {TotalAmount}",
            run.Id,
            userAccountId,
            totalSucceeded,
            totalAmount);

        logger.LogInformation(
            "Creating RUN_COMPLETED notification for user account {UserAccountId} in run {TopUpRunId}",
            userAccountId,
            run.Id);

        Result<long> create = await notificationWriter.CreateAsync(
            new NotificationCreateRequest(
                userAccountId,
                NotificationTypeCode.RunCompleted,
                $"Top-up Run {run.Id} Completed",
                $"Total Succeeded: {totalSucceeded} student(s). Total amount processed: {totalAmount:0.00}."),
            cancellationToken);

        if (create.IsFailure)
        {
            logger.LogWarning(
                "Failed to create RUN_COMPLETED notification for user account {UserAccountId} in run {TopUpRunId}: {ErrorCode}",
                userAccountId,
                run.Id,
                create.Error.Code);
        }
        else
        {
            logger.LogInformation(
                "RUN_COMPLETED notification created successfully with NotificationId {NotificationId} for user account {UserAccountId} in run {TopUpRunId}",
                create.Value,
                userAccountId,
                run.Id);
        }
    }
}

