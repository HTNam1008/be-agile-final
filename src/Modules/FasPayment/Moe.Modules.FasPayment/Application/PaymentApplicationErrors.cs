using Moe.SharedKernel.Results;

namespace Moe.Modules.FasPayment.Application;

internal static class PaymentApplicationErrors
{
    public static readonly Error StudentRequired = new("PAYMENT.STUDENT_REQUIRED", "An authenticated student is required.");
    public static readonly Error EnrollmentNotFound = new(
        "PAYMENT.ENROLLMENT_NOT_FOUND",
        "One or more course enrollments were not found.");
    public static readonly Error AdministratorRequired = new("PAYMENT.ADMIN_REQUIRED", "An authenticated administrator is required.");
    public static readonly Error CourseNotFound = new("PAYMENT.COURSE_NOT_FOUND", "The course was not found.");
    public static readonly Error CourseForbidden = new("PAYMENT.COURSE_FORBIDDEN", "The administrator cannot manage this course.");
    public static readonly Error BillNotFound = new("PAYMENT.BILL_NOT_FOUND", "The payable bill was not found.");
    public static readonly Error CancellationNotAllowed = new(
        "PAYMENT.CANCELLATION_NOT_ALLOWED",
        "This enrollment cannot be cancelled under the configured course refund policy.");
    public static readonly Error IdempotencyKeyRequired = new(
        "PAYMENT.IDEMPOTENCY_KEY_REQUIRED",
        "An idempotency key is required to cancel an enrollment.");
}
