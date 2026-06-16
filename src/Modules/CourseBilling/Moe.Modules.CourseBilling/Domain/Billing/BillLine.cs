using Moe.SharedKernel.Domain;

namespace Moe.Modules.CourseBilling.Domain.Billing;

internal sealed class BillLine : Entity<long>
{
    private BillLine() : base(0) { }

    public long BillId { get; private set; }
    public long FeeComponentId { get; private set; }
    public long? CourseFeeId { get; private set; }
    public string DescriptionSnapshot { get; private set; } = string.Empty;
    public decimal Quantity { get; private set; }
    public decimal UnitAmount { get; private set; }
    public decimal GrossAmount { get; private set; }
    public decimal SubsidyAmount { get; private set; }
    public decimal NetAmount { get; private set; }
}
