namespace Moe.Modules.CourseBilling.Contracts.AdminCourses;

public sealed record CourseEnrollmentSummaryDto(int PendingPaymentCount, int CancelledCount, int TotalCount);

public sealed record AdminCourseEnrollmentDto(
    long CourseEnrollmentId,
    long CourseId,
    long PersonId,
    string? FullName,
    string EnrollmentSourceCode,
    long EnrolledByLoginAccountId,
    DateTime EnrolledAt,
    string EnrollmentStatusCode);
