using Moe.Modules.EducationAccountTopUp.Domain.TopUps;

namespace Moe.Modules.EducationAccountTopUp.Application.RunExecution;

public sealed record RecipientProcessingResult
{
    public required long TopUpTransactionId { get; init; }
    public required string Status { get; init; }
    public required decimal CreditedAmount { get; init; }
    public long? AccountTransactionId { get; init; }
    public string? Reason { get; init; }
    public bool AlreadyProcessed { get; init; }

    public static RecipientProcessingResult Completed(
        long transactionId,
        long accountTransactionId,
        decimal amount,
        bool alreadyProcessed)
    {
        return new RecipientProcessingResult
        {
            TopUpTransactionId = transactionId,
            Status = TopUpTransactionStatusCodes.Completed,
            CreditedAmount = amount,
            AccountTransactionId = accountTransactionId,
            AlreadyProcessed = alreadyProcessed
        };
    }

    public static RecipientProcessingResult Failed(long transactionId, string reason)
    {
        return new RecipientProcessingResult
        {
            TopUpTransactionId = transactionId,
            Status = TopUpTransactionStatusCodes.Failed,
            CreditedAmount = 0m,
            Reason = reason
        };
    }

    public static RecipientProcessingResult Skipped(long transactionId, string reason)
    {
        return new RecipientProcessingResult
        {
            TopUpTransactionId = transactionId,
            Status = TopUpTransactionStatusCodes.Skipped,
            CreditedAmount = 0m,
            Reason = reason
        };
    }

    public static RecipientProcessingResult FromExisting(TopUpTransaction transaction)
    {
        return new RecipientProcessingResult
        {
            TopUpTransactionId = transaction.Id,
            Status = transaction.TransactionStatusCode,
            CreditedAmount = transaction.Amount,
            AccountTransactionId = transaction.AccountTransactionId,
            Reason = transaction.Reason,
            AlreadyProcessed = true
        };
    }
}
