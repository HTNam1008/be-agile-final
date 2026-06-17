using Moe.SharedKernel.Domain;

namespace Moe.Modules.EducationAccountTopUp.Domain.TopUps;

internal sealed class TopUpTransaction : Entity<long>
{
    private TopUpTransaction() : base(0) { }

    public long TopUpRunId { get; private set; }
    public long EducationAccountId { get; private set; }
    public decimal TopUpAmount { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public string TransactionStatusCode { get; private set; } = string.Empty;
    public long? ProcessedByLoginAccountId { get; private set; }
    public DateTime? ProcessedAtUtc { get; private set; }
    public string? FailureReason { get; private set; }
    public long? AccountTransactionId { get; private set; }
    public string IdempotencyKey { get; private set; } = string.Empty;

    public static TopUpTransaction Create(
        long topUpRunId,
        long educationAccountId,
        decimal topUpAmount,
        string reason,
        string idempotencyKey)
    {
        return new TopUpTransaction
        {
            TopUpRunId = topUpRunId,
            EducationAccountId = educationAccountId,
            TopUpAmount = topUpAmount,
            Reason = reason,
            TransactionStatusCode = "PENDING",
            IdempotencyKey = idempotencyKey
        };
    }

    public void MarkCompleted(long accountTransactionId, long currentUserId, DateTime nowUtc)
    {
        TransactionStatusCode = "COMPLETED";
        AccountTransactionId = accountTransactionId;
        ProcessedByLoginAccountId = currentUserId;
        ProcessedAtUtc = nowUtc;
    }

    public void MarkFailed(string failureReason, long currentUserId, DateTime nowUtc)
    {
        TransactionStatusCode = "FAILED";
        FailureReason = failureReason;
        ProcessedByLoginAccountId = currentUserId;
        ProcessedAtUtc = nowUtc;
    }

    public void MarkSkipped(string skipReason, long currentUserId, DateTime nowUtc)
    {
        TransactionStatusCode = "SKIPPED";
        FailureReason = skipReason;
        ProcessedByLoginAccountId = currentUserId;
        ProcessedAtUtc = nowUtc;
    }
}
