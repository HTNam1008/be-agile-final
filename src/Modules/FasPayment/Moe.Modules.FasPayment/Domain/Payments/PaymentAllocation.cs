using Moe.SharedKernel.Domain;

namespace Moe.Modules.FasPayment.Domain.Payments;

internal sealed class PaymentAllocation : Entity<long>
{
    private PaymentAllocation() : base(0) { }

    public PaymentAllocation(
        long paymentId,
        long billId,
        long billingStatementItemId,
        decimal amount,
        DateTime createdAtUtc) : base(0)
    {
        PaymentId = paymentId;
        BillId = billId;
        BillingStatementItemId = billingStatementItemId;
        AllocatedAmount = amount;
        AllocationStatusCode = "PENDING";
        CreatedAtUtc = createdAtUtc;
    }

    public long PaymentId { get; private set; }
    public long BillId { get; private set; }
    public long BillingStatementItemId { get; private set; }
    public decimal AllocatedAmount { get; private set; }
    public string AllocationStatusCode { get; private set; } = string.Empty;
    public DateTime CreatedAtUtc { get; private set; }

    public void MarkApplied() => AllocationStatusCode = "APPLIED";

    public void AttachToPayment(long paymentId)
    {
        if (paymentId <= 0) throw new ArgumentOutOfRangeException(nameof(paymentId));
        PaymentId = paymentId;
    }
}
