using Moe.SharedKernel.Domain;
using Moe.SharedKernel.Results;

namespace Moe.Modules.CourseBilling.Domain.Billing;

internal sealed class Bill : Entity<long>
{
    private Bill() : base(0) { }

    private Bill(
        long courseEnrollmentId,
        string billNumber,
        DateTime issuedAtUtc,
        DateOnly dueDate,
        decimal grossAmount,
        decimal subsidyAmount) : base(0)
    {
        CourseEnrollmentId = courseEnrollmentId;
        BillNumber = billNumber;
        IssuedAtUtc = issuedAtUtc;
        DueDate = dueDate;
        GrossAmount = Money(grossAmount);
        SubsidyAmount = Money(subsidyAmount);
        NetPayableAmount = Money(GrossAmount - SubsidyAmount);
        PaidAmount = 0m;
        OutstandingAmount = NetPayableAmount;
        BillStatusCode = OutstandingAmount == 0m ? BillStatusCodes.Paid : BillStatusCodes.Issued;
    }

    public string BillNumber { get; private set; } = string.Empty;
    public long CourseEnrollmentId { get; private set; }
    public DateTime IssuedAtUtc { get; private set; }
    public DateOnly DueDate { get; private set; }
    public decimal GrossAmount { get; private set; }
    public decimal SubsidyAmount { get; private set; }
    public decimal NetPayableAmount { get; private set; }
    public decimal PaidAmount { get; private set; }
    public decimal OutstandingAmount { get; private set; }
    public string BillStatusCode { get; private set; } = string.Empty;

    public static Result<Bill> IssueForCourseEnrollment(
        long courseEnrollmentId,
        string billNumber,
        DateTime issuedAtUtc,
        DateOnly dueDate,
        decimal grossAmount,
        decimal subsidyAmount = 0m)
    {
        if (courseEnrollmentId <= 0)
        {
            return Result<Bill>.Failure(BillingErrors.InvalidCourseEnrollment);
        }

        if (string.IsNullOrWhiteSpace(billNumber))
        {
            return Result<Bill>.Failure(BillingErrors.InvalidBillNumber);
        }

        if (grossAmount < 0m || subsidyAmount < 0m || subsidyAmount > grossAmount)
        {
            return Result<Bill>.Failure(BillingErrors.InvalidBillAmount);
        }

        Bill bill = new(
            courseEnrollmentId,
            billNumber.Trim().ToUpperInvariant(),
            issuedAtUtc,
            dueDate,
            grossAmount,
            subsidyAmount);

        return Result<Bill>.Success(bill);
    }

    public Result Cancel()
    {
        if (BillStatusCode == BillStatusCodes.Paid)
        {
            return Result.Failure(BillingErrors.PaidBillCannotBeCancelled);
        }

        BillStatusCode = BillStatusCodes.Cancelled;
        OutstandingAmount = 0m;
        return Result.Success();
    }

    private static decimal Money(decimal amount)
        => decimal.Round(amount, 2, MidpointRounding.AwayFromZero);
}

public static class BillStatusCodes
{
    public const string Issued = "ISSUED";
    public const string Paid = "PAID";
    public const string Cancelled = "CANCELLED";
}

public static class BillingErrors
{
    public static readonly Error InvalidCourseEnrollment = new("BILL.INVALID_COURSE_ENROLLMENT", "A valid course enrollment is required.");
    public static readonly Error InvalidBillNumber = new("BILL.INVALID_NUMBER", "A valid bill number is required.");
    public static readonly Error InvalidBillAmount = new("BILL.INVALID_AMOUNT", "Bill amounts are invalid.");
    public static readonly Error InvalidBillLine = new("BILL.INVALID_LINE", "A valid bill line is required.");
    public static readonly Error PaidBillCannotBeCancelled = new("BILL.PAID_CANNOT_CANCEL", "A paid bill cannot be cancelled.");
}
