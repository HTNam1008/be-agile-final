namespace Moe.Modules.CourseBilling.IGateway.Repositories;

internal sealed record StudentDashboardCourseSummary(
    long? CourseEnrollmentId,
    long? CoursePaymentPlanId,
    long CourseId,
    string CourseCode,
    string CourseName,
    string? LecturerName,
    bool HasActiveFee,
    decimal TotalFee,
    int MaterialCount,
    DateOnly StartDate,
    DateOnly? EndDate,
    string EnrollmentStatusCode);
