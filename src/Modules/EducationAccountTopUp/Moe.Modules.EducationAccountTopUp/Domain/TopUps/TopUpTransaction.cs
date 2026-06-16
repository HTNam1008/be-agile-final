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
}
