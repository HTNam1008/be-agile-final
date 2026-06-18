using Microsoft.Extensions.Logging;
using Moe.Application.Abstractions.Clock;
using Moe.Application.Abstractions.Persistence;
using Moe.Modules.EducationAccountTopUp.Domain.TopUps;
using Moe.Modules.EducationAccountTopUp.IGateway;
using Moe.Modules.EducationAccountTopUp.IGateway.Repositories;
using Moe.SharedKernel.Results;

namespace Moe.Modules.EducationAccountTopUp.Application.RunExecution;

public sealed class RecipientProcessingService(
    ITopUpTransactionRepository transactions,
    IAccountCreditGateway accountCreditGateway,
    IRecipientValidator recipientValidator,
    IUnitOfWork unitOfWork,
    IClock clock,
    ILogger<RecipientProcessingService> logger)
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
            return Result<RecipientProcessingResult>.Success(
                RecipientProcessingResult.FromExisting(transaction));
        }

        if (transaction is null)
        {
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
            Result skip = transaction.Skip(validation.Error.Message, utcNow);
            if (skip.IsFailure)
            {
                return Result<RecipientProcessingResult>.Failure(skip.Error);
            }

            await unitOfWork.SaveChangesAsync(cancellationToken);

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
                return await FailTransactionAsync(
                    transaction,
                    credit.Error.Message,
                    utcNow,
                    cancellationToken);
            }

            Result complete = transaction.Complete(credit.Value.AccountTransactionId, utcNow);
            if (complete.IsFailure)
            {
                return Result<RecipientProcessingResult>.Failure(complete.Error);
            }

            await unitOfWork.SaveChangesAsync(cancellationToken);

            return Result<RecipientProcessingResult>.Success(
                RecipientProcessingResult.Completed(
                    transaction.Id,
                    credit.Value.AccountTransactionId,
                    amount,
                    credit.Value.AlreadyProcessed));
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Failed to credit account {EducationAccountId} for top-up run {TopUpRunId}",
                educationAccountId,
                topUpRunId);

            return await FailTransactionAsync(
                transaction,
                CreditUnavailableReason,
                utcNow,
                cancellationToken);
        }
    }

    private async Task<Result<RecipientProcessingResult>> FailTransactionAsync(
        TopUpTransaction transaction,
        string reason,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        Result fail = transaction.Fail(reason, utcNow);
        if (fail.IsFailure)
        {
            return Result<RecipientProcessingResult>.Failure(fail.Error);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<RecipientProcessingResult>.Success(
            RecipientProcessingResult.Failed(transaction.Id, reason));
    }
}
