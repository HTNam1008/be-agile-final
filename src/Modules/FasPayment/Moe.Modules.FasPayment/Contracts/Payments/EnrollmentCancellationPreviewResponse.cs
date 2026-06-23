namespace Moe.Modules.FasPayment.Contracts.Payments;

public sealed record EnrollmentCancellationPreviewResponse(
    long CourseEnrollmentId,
    long CourseId,
    string CourseCode,
    string CourseName,
    bool CanCancel,
    string PolicyPeriodCode,
    decimal RefundPercentage,
    decimal PaidAmount,
    decimal RefundAmount,
    decimal EducationAccountRefundAmount,
    decimal OnlineRefundAmount,
    DateOnly CourseStartDate,
    DateOnly CourseEndDate,
    string? CannotCancelReason);

public static class RefundPolicyPeriodCodes
{
    public const string BeforeCourseStart = "BEFORE_COURSE_START";
    public const string DuringCourse = "DURING_COURSE";
    public const string CourseEnded = "COURSE_ENDED";
}
