using Moe.SharedKernel.Domain;

namespace Moe.Modules.FasPayment.Domain.Payments;

internal sealed class Payment : Entity<long>
{
    private Payment() : base(0) { }

    public string PaymentNumber { get; private set; } = string.Empty;
    public long BillId { get; private set; }
    public long PayerPersonId { get; private set; }
    public decimal PaymentAmount { get; private set; }
    public decimal SuccessfulAmount { get; private set; }
    public string PaymentStatusCode { get; private set; } = string.Empty;
    public DateTime InitiatedAtUtc { get; private set; }
    public DateTime? CompletedAtUtc { get; private set; }
    public string IdempotencyKey { get; private set; } = string.Empty;
    public string? ReceiptNumber { get; private set; }
}
