using Moe.SharedKernel.Domain;

namespace Moe.Modules.CourseBilling.Domain.Billing;

internal sealed class BillDeferral : Entity<long>
{
    private BillDeferral() : base(0) { }

    public BillDeferral(
        long billId,
        long courseEnrollmentId,
        long? sourcePaymentId,
        DateOnly fromDueDate,
        DateOnly toDueDate,
        decimal deferredAmount,
        int sequenceNumber,
        long createdByLoginAccountId,
        DateTime createdAtUtc) : base(0)
    {
        BillId = billId;
        CourseEnrollmentId = courseEnrollmentId;
        SourcePaymentId = sourcePaymentId;
        FromDueDate = fromDueDate;
        ToDueDate = toDueDate;
        DeferredAmount = deferredAmount;
        DeferralSequenceNumber = sequenceNumber;
        ReasonCode = sourcePaymentId is null
            ? "STUDENT_DEFER_REQUEST"
            : "ONLINE_PAYMENT_NOT_COMPLETED";
        CreatedByLoginAccountId = createdByLoginAccountId;
        CreatedAtUtc = createdAtUtc;
    }

    public long BillId { get; private set; }
    public long CourseEnrollmentId { get; private set; }
    public long? SourcePaymentId { get; private set; }
    public DateOnly FromDueDate { get; private set; }
    public DateOnly ToDueDate { get; private set; }
    public decimal DeferredAmount { get; private set; }
    public int DeferralSequenceNumber { get; private set; }
    public string ReasonCode { get; private set; } = string.Empty;
    public long CreatedByLoginAccountId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
}
