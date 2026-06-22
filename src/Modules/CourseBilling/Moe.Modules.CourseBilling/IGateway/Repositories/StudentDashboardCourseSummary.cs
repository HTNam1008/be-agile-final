namespace Moe.Modules.CourseBilling.IGateway.Repositories;

internal sealed record StudentDashboardCourseSummary(
    long? CourseEnrollmentId,
    long CourseId,
    string CourseCode,
    string CourseName,
    string? LecturerName,
    DateOnly StartDate,
    DateOnly? EndDate,
    string EnrollmentStatusCode);
