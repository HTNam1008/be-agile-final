namespace Moe.Modules.CourseBilling.Domain.Courses;

internal static class CourseEnrollmentStatusCodes
{
    public const string PendingPlanSelection = "PENDING_PLAN_SELECTION";
    public const string PendingPayment = "PENDING_PAYMENT";
    public const string Active = "ACTIVE";
    public const string PaymentPastDue = "PAYMENT_PAST_DUE";
    public const string PaidInFull = "PAID_IN_FULL";
    public const string Refunded = "REFUNDED";
    public const string Cancelled = "CANCELLED";
    public const string Completed = "COMPLETED";
    public const string Exited = "EXITED";
}
