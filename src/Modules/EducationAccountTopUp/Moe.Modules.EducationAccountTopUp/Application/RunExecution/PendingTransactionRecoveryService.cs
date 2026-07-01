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

public sealed class PendingTransactionRecoveryService(
    ITopUpTransactionRepository transactions,
    ITopUpRunRepository runs,
    IAccountCreditGateway accountCreditGateway,
    ITopUpExecutionEventPublisher events,
    ITopUpExecutionMetrics metrics,
    IEducationAccountRepository educationAccounts,
    IStudentNotificationRecipientResolver notificationRecipientResolver,
    INotificationWriter notificationWriter,
    IUnitOfWork unitOfWork,
    IClock clock,
    ILogger<PendingTransactionRecoveryService> logger) : IPendingTransactionRecoveryService
{
    private const int RecoveryPageSize = 500;

    public async Task<int> RecoverPendingTransactionsAsync(
        long topUpRunId,
        string campaignReason,
        CancellationToken cancellationToken = default)
    {
        int recovered = 0;
        decimal recoveredAmount = 0m;
        int skip = 0;

        while (true)
        {
            IReadOnlyList<TopUpTransaction> pendingBatch = await transactions.GetPendingByRunIdPagedAsync(
                topUpRunId,
                skip,
                RecoveryPageSize,
                cancellationToken);

            if (pendingBatch.Count == 0)
            {
                break;
            }

            if (skip == 0)
            {
                logger.LogInformation(
                    "Recovering pending top-up transactions for run {TopUpRunId}; first batch of {BatchSize} pending",
                    topUpRunId,
                    pendingBatch.Count);
            }

            foreach (TopUpTransaction transaction in pendingBatch)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

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

                            if (!credit.Value.AlreadyProcessed)
                            {
                                await CreateTopUpReceivedNotificationAsync(
                                    topUpRunId,
                                    transaction.EducationAccountId,
                                    transaction.Amount,
                                    completedAtUtc,
                                    cancellationToken);
                            }

                            metrics.RecordRecipientProcessed(
                                topUpRunId,
                                TopUpTransactionStatusCodes.Completed,
                                credit.Value.AlreadyProcessed,
                                accountCreditFailure: false);

                            recovered++;
                            recoveredAmount += transaction.Amount;

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

            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            skip += pendingBatch.Count;

            if (pendingBatch.Count < RecoveryPageSize)
            {
                break;
            }
        }

        if (recovered > 0)
        {
            TopUpRun? run = await runs.GetByIdAsync(topUpRunId, cancellationToken);
            if (run is not null)
            {
                int totalProcessed = run.TotalProcessed + recovered;
                int totalSucceeded = run.TotalSucceeded + recovered;
                int totalFailed = run.TotalFailed;
                int totalSkipped = run.TotalSkipped;
                decimal totalAmount = run.TotalAmount + recoveredAmount;

                run.ReconcileCounters(totalProcessed, totalSucceeded, totalFailed, totalSkipped, totalAmount);
                await unitOfWork.SaveChangesAsync(cancellationToken);

                logger.LogInformation(
                    "Reconciled run {TopUpRunId} after recovery: {TotalRecovered} transactions recovered, {TotalAmount} additional amount",
                    topUpRunId,
                    recovered,
                    recoveredAmount);
            }
        }

        return recovered;
    }

    private async Task CreateTopUpReceivedNotificationAsync(
        long topUpRunId,
        long educationAccountId,
        decimal amount,
        DateTime completedAtUtc,
        CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "CreateTopUpReceivedNotificationAsync started for recovered run {TopUpRunId}, education account {EducationAccountId}",
            topUpRunId,
            educationAccountId);

        EducationAccount? account = await educationAccounts.FindByIdAsync(
            educationAccountId,
            cancellationToken);

        if (account is null)
        {
            logger.LogWarning(
                "Skipping top-up notification for recovered run {TopUpRunId}; education account {EducationAccountId} not found",
                topUpRunId,
                educationAccountId);
            return;
        }

        long? userAccountId = await notificationRecipientResolver.FindUserAccountIdByPersonIdAsync(
            account.PersonId,
            cancellationToken);

        if (userAccountId is null)
        {
            logger.LogWarning(
                "Skipping top-up notification for recovered run {TopUpRunId}; no user account found for person {PersonId}",
                topUpRunId,
                account.PersonId);
            return;
        }

        string amountText = amount.ToString("0.00");

        logger.LogInformation(
            "Creating TOP_UP_RECEIVED notification for recovered run {TopUpRunId} and user account {UserAccountId}",
            topUpRunId,
            userAccountId.Value);

        Result<long> create = await notificationWriter.CreateAsync(
            new NotificationCreateRequest(
                userAccountId.Value,
                NotificationTypeCode.TopUpReceived,
                $"Top-up Credited: {account.AccountNumber}",
                $"Amount {amountText} has been credited to account {account.AccountNumber}."),
            cancellationToken);

        if (create.IsFailure)
        {
            logger.LogWarning(
                "Failed to create top-up notification for recovered run {TopUpRunId} and user account {UserAccountId}: {ErrorCode}",
                topUpRunId,
                userAccountId.Value,
                create.Error.Code);
        }
        else
        {
            logger.LogInformation(
                "TOP_UP_RECEIVED notification created successfully with NotificationId {NotificationId} for user account {UserAccountId} in recovered run {TopUpRunId}",
                create.Value,
                userAccountId.Value,
                topUpRunId);
        }
    }
}
