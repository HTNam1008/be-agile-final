using Moe.SharedKernel.Domain;

namespace Moe.Modules.FasPayment.Domain.Payments;

internal sealed class PaymentPart : Entity<long>
{
    private PaymentPart() : base(0) { }

    public long PaymentId { get; private set; }
    public int SequenceNumber { get; private set; }
    public string PaymentMethodCode { get; private set; } = string.Empty;
    public long? EducationAccountId { get; private set; }
    public long? AccountTransactionId { get; private set; }
    public decimal PartAmount { get; private set; }
    public string? ProviderCode { get; private set; }
    public string? ProviderReference { get; private set; }
    public string PartStatusCode { get; private set; } = string.Empty;
    public DateTime? AuthorizedAtUtc { get; private set; }
    public DateTime? SettledAtUtc { get; private set; }
    public string? FailureReason { get; private set; }
}
