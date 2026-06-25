using Moe.SharedKernel.Domain;
using Moe.SharedKernel.Results;

namespace Moe.Modules.CourseBilling.Domain.Billing;

internal sealed class BillLine : Entity<long>
{
    private BillLine() : base(0) { }

    private BillLine(
        long billId,
        long feeComponentId,
        long? courseFeeId,
        string descriptionSnapshot,
        decimal quantity,
        decimal unitAmount,
        decimal subsidyAmount) : base(0)
    {
        BillId = billId;
        FeeComponentId = feeComponentId;
        CourseFeeId = courseFeeId;
        DescriptionSnapshot = descriptionSnapshot;
        Quantity = quantity;
        UnitAmount = unitAmount;
        GrossAmount = Money(quantity * unitAmount);
        SubsidyAmount = Money(subsidyAmount);
        NetAmount = Money(GrossAmount - SubsidyAmount);
    }

    public long BillId { get; private set; }
    public long FeeComponentId { get; private set; }
    public long? CourseFeeId { get; private set; }
    public string DescriptionSnapshot { get; private set; } = string.Empty;
    public decimal Quantity { get; private set; }
    public decimal UnitAmount { get; private set; }
    public decimal GrossAmount { get; private set; }
    public decimal SubsidyAmount { get; private set; }
    public decimal NetAmount { get; private set; }

    public static Result<BillLine> FromCourseFee(
        long billId,
        long feeComponentId,
        long courseFeeId,
        string descriptionSnapshot,
        decimal feeValue,
        decimal subsidyAmount = 0m)
    {
        if (billId <= 0 || feeComponentId <= 0 || courseFeeId <= 0 || feeValue < 0m ||
            subsidyAmount < 0m || subsidyAmount > feeValue)
        {
            return Result<BillLine>.Failure(BillingErrors.InvalidBillLine);
        }

        if (string.IsNullOrWhiteSpace(descriptionSnapshot))
        {
            return Result<BillLine>.Failure(BillingErrors.InvalidBillLine);
        }

        BillLine line = new(
            billId,
            feeComponentId,
            courseFeeId,
            descriptionSnapshot.Trim(),
            1m,
            feeValue,
            subsidyAmount);

        return Result<BillLine>.Success(line);
    }

    private static decimal Money(decimal amount)
        => decimal.Round(amount, 2, MidpointRounding.AwayFromZero);
}
