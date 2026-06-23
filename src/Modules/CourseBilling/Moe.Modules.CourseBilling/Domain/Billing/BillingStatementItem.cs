using Moe.SharedKernel.Domain;

namespace Moe.Modules.CourseBilling.Domain.Billing;

internal sealed class BillingStatementItem : Entity<long>
{
    private BillingStatementItem() : base(0) { }

    public BillingStatementItem(
        long billingStatementId,
        long billId,
        decimal includedAmount,
        DateTime createdAtUtc) : base(0)
    {
        BillingStatementId = billingStatementId;
        BillId = billId;
        IncludedAmount = includedAmount;
        ItemStatusCode = "OPEN";
        CreatedAtUtc = createdAtUtc;
    }

    public long BillingStatementId { get; private set; }
    public long BillId { get; private set; }
    public decimal IncludedAmount { get; private set; }
    public decimal PaidAmount { get; private set; }
    public string ItemStatusCode { get; private set; } = string.Empty;
    public DateTime CreatedAtUtc { get; private set; }

    public void Refresh(decimal includedAmount, decimal paidAmount)
    {
        IncludedAmount = includedAmount;
        PaidAmount = paidAmount;
        ItemStatusCode = paidAmount >= includedAmount ? "PAID" : paidAmount > 0m ? "PARTIALLY_PAID" : "OPEN";
    }
}
