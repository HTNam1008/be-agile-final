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

public sealed class RecipientProcessingService(
    ITopUpTransactionRepository transactions,
    IAccountCreditGateway accountCreditGateway,
    IRecipientValidator recipientValidator,
    ITopUpExecutionEventPublisher events,
    ITopUpExecutionMetrics metrics,
    IEducationAccountRepository educationAccounts,
    IStudentNotificationRecipientResolver notificationRecipientResolver,
    INotificationWriter notificationWriter,
    IUnitOfWork unitOfWork,
    IClock clock,
    ILogger<RecipientProcessingService> logger) : IRecipientProcessingService
{
    private const string CreditUnavailableReason = "Credit service unavailable";

    public async Task<Result<RecipientProcessingResult>> ProcessRecipientAsync(
        long topUpRunId,
        long educationAccountId,
        decimal amount,
        long organizationUnitId,
        string campaignReason,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "ProcessRecipientAsync started for run {TopUpRunId}, education account {EducationAccountId}, amount {Amount}, organization unit {OrganizationUnitId}",
            topUpRunId,
            educationAccountId,
            amount,
            organizationUnitId);

        if (amount <= 0)
        {
            return Result<RecipientProcessingResult>.Failure(TopUpErrors.InvalidCreditAmount);
        }

        DateTime utcNow = clock.UtcNow.UtcDateTime;
        string idempotencyKey = $"topup:{topUpRunId}:{educationAccountId}";

        TopUpTransaction? transaction = await transactions.GetByIdempotencyKeyAsync(
            idempotencyKey,
            cancellationToken);

        if (transaction is not null
            && transaction.TransactionStatusCode != TopUpTransactionStatusCodes.Pending)
        {
            logger.LogInformation(
                "Existing non-pending transaction found for idempotency key {IdempotencyKey}: status {Status}",
                idempotencyKey,
                transaction.TransactionStatusCode);

            metrics.RecordRecipientProcessed(
                topUpRunId,
                transaction.TransactionStatusCode,
                duplicateIdempotencyHit: true,
                accountCreditFailure: false);

            return Result<RecipientProcessingResult>.Success(
                RecipientProcessingResult.FromExisting(transaction));
        }

        if (transaction is null)
        {
            logger.LogInformation(
                "Creating new top-up transaction for run {TopUpRunId}, education account {EducationAccountId}, idempotency key {IdempotencyKey}",
                topUpRunId,
                educationAccountId,
                idempotencyKey);

            transaction = TopUpTransaction.Create(topUpRunId, educationAccountId, amount, utcNow);
            transactions.Add(transaction);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        Result validation = await recipientValidator.ValidateRecipientAsync(
            educationAccountId,
            organizationUnitId,
            cancellationToken);

        if (validation.IsFailure)
        {
            logger.LogWarning(
                "Recipient validation failed for education account {EducationAccountId} in run {TopUpRunId}: {ErrorCode} - {ErrorMessage}",
                educationAccountId,
                topUpRunId,
                validation.Error.Code,
                validation.Error.Message);

            Result skip = transaction.Skip(validation.Error.Message, utcNow);
            if (skip.IsFailure)
            {
                return Result<RecipientProcessingResult>.Failure(skip.Error);
            }

            await unitOfWork.SaveChangesAsync(cancellationToken);

            metrics.RecordRecipientProcessed(
                topUpRunId,
                TopUpTransactionStatusCodes.Skipped,
                duplicateIdempotencyHit: false,
                accountCreditFailure: false);

            return Result<RecipientProcessingResult>.Success(
                RecipientProcessingResult.Skipped(transaction.Id, validation.Error.Message));
        }

        try
        {
            Result<CreditAccountResult> credit = await accountCreditGateway.CreditAccountForTopUpAsync(
                educationAccountId,
                amount,
                idempotencyKey,
                campaignReason,
                cancellationToken);

            if (credit.IsFailure)
            {
                logger.LogWarning(
                    "Credit gateway failed for education account {EducationAccountId} in run {TopUpRunId}: {ErrorCode} - {ErrorMessage}",
                    educationAccountId,
                    topUpRunId,
                    credit.Error.Code,
                    credit.Error.Message);

                metrics.RecordRecipientProcessed(
                    topUpRunId,
                    TopUpTransactionStatusCodes.Failed,
                    duplicateIdempotencyHit: false,
                    accountCreditFailure: true);

                return await FailTransactionAsync(
                    transaction,
                    educationAccountId,
                    credit.Error.Message,
                    utcNow,
                    cancellationToken);
            }

            Result complete = transaction.Complete(credit.Value.AccountTransactionId, utcNow);
            if (complete.IsFailure)
            {
                return Result<RecipientProcessingResult>.Failure(complete.Error);
            }

            logger.LogInformation(
                "Transaction completed for education account {EducationAccountId} in run {TopUpRunId}. AccountTransactionId={AccountTransactionId}, AlreadyProcessed={AlreadyProcessed}",
                educationAccountId,
                topUpRunId,
                credit.Value.AccountTransactionId,
                credit.Value.AlreadyProcessed);

            await unitOfWork.SaveChangesAsync(cancellationToken);

            await events.PublishTopUpReceivedAsync(
                new TopUpReceivedReport
                {
                    TopUpRunId = topUpRunId,
                    TopUpTransactionId = transaction.Id,
                    EducationAccountId = educationAccountId,
                    AccountTransactionId = credit.Value.AccountTransactionId,
                    Amount = amount,
                    AlreadyProcessed = credit.Value.AlreadyProcessed,
                    OccurredAtUtc = utcNow
                },
                cancellationToken);

            if (!credit.Value.AlreadyProcessed)
            {
                await CreateTopUpReceivedNotificationAsync(
                    topUpRunId,
                    educationAccountId,
                    amount,
                    utcNow,
                    cancellationToken);
            }

            metrics.RecordRecipientProcessed(
                topUpRunId,
                TopUpTransactionStatusCodes.Completed,
                credit.Value.AlreadyProcessed,
                accountCreditFailure: false);

            return Result<RecipientProcessingResult>.Success(
                RecipientProcessingResult.Completed(
                    transaction.Id,
                    credit.Value.AccountTransactionId,
                    amount,
                    credit.Value.AlreadyProcessed));
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
                "Failed to credit account {EducationAccountId} for top-up run {TopUpRunId}",
                educationAccountId,
                topUpRunId);

            return await FailTransactionAsync(
                transaction,
                educationAccountId,
                CreditUnavailableReason,
                utcNow,
                cancellationToken);
        }
    }

    private async Task CreateTopUpReceivedNotificationAsync(
        long topUpRunId,
        long educationAccountId,
        decimal amount,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "CreateTopUpReceivedNotificationAsync started for run {TopUpRunId}, education account {EducationAccountId}",
            topUpRunId,
            educationAccountId);

        EducationAccount? account = await educationAccounts.FindByIdAsync(
            educationAccountId,
            cancellationToken);

        if (account is null)
        {
            logger.LogWarning(
                "Skipping top-up notification for education account {EducationAccountId}; account not found",
                educationAccountId);
            return;
        }

        long? userAccountId = await notificationRecipientResolver.FindUserAccountIdByPersonIdAsync(
            account.PersonId,
            cancellationToken);

        if (userAccountId is null)
        {
            logger.LogWarning(
                "Skipping top-up notification for education account {EducationAccountId}; no user account found for person {PersonId}",
                educationAccountId,
                account.PersonId);
            return;
        }

        string amountText = amount.ToString("0.00");
        string accountNumber = account.AccountNumber;

        logger.LogInformation(
            "Creating TOP_UP_RECEIVED notification for user account {UserAccountId} from education account {EducationAccountId} in run {TopUpRunId}",
            userAccountId.Value,
            educationAccountId,
            topUpRunId);

        bool created = await notificationWriter.CreateForBusinessFlowAsync(
            new NotificationCreateRequest(
                userAccountId.Value,
                NotificationTypeCode.TopUpReceived,
                $"Top-up Credited: {accountNumber}",
                $"Amount {amountText} has been credited to account {accountNumber}."),
            logger,
            "Top-up recipient credited",
            cancellationToken);

        if (created)
        {
            logger.LogInformation(
                "TOP_UP_RECEIVED notification created successfully for user account {UserAccountId} in run {TopUpRunId}",
                userAccountId.Value,
                topUpRunId);
        }
    }
    private async Task<Result<RecipientProcessingResult>> FailTransactionAsync(
        TopUpTransaction transaction,
        long educationAccountId,
        string reason,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "FailTransactionAsync started for run {TopUpRunId}, education account {EducationAccountId}, reason {Reason}",
            transaction.TopUpRunId,
            educationAccountId,
            reason);

        Result fail = transaction.Fail(reason, utcNow);
        if (fail.IsFailure)
        {
            return Result<RecipientProcessingResult>.Failure(fail.Error);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        await CreateTopUpFailureNotificationAsync(
            transaction.TopUpRunId,
            educationAccountId,
            reason,
            cancellationToken);

        return Result<RecipientProcessingResult>.Success(
            RecipientProcessingResult.Failed(transaction.Id, reason));
    }

    private async Task CreateTopUpFailureNotificationAsync(
        long topUpRunId,
        long educationAccountId,
        string reason,
        CancellationToken cancellationToken)
    {
        EducationAccount? account = await educationAccounts.FindByIdAsync(
            educationAccountId,
            cancellationToken);

        if (account is null)
        {
            logger.LogWarning(
                "Skipping TOP_UP_FAILURE notification for education account {EducationAccountId}; account not found",
                educationAccountId);
            return;
        }

        long? userAccountId = await notificationRecipientResolver.FindUserAccountIdByPersonIdAsync(
            account.PersonId,
            cancellationToken);

        if (userAccountId is null)
        {
            logger.LogWarning(
                "Skipping TOP_UP_FAILURE notification for education account {EducationAccountId}; no user account found for person {PersonId}",
                educationAccountId,
                account.PersonId);
            return;
        }

            await notificationWriter.CreateForBusinessFlowAsync(
                new NotificationCreateRequest(
                    userAccountId.Value,
                    NotificationTypeCode.TopUpFailure,
                    "Top-up Transfer Failed",
                    $"Top-up for account {account.AccountNumber} failed. Reason: {reason}. Please contact the administrator."),
                logger,
                "Top-up recipient failed",
                cancellationToken);
    }
}


