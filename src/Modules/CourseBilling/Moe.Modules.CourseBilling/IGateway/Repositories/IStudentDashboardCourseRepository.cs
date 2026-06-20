namespace Moe.Modules.CourseBilling.IGateway.Repositories;

internal interface IStudentDashboardCourseRepository
{
    Task<IReadOnlyCollection<StudentDashboardCourseSummary>> ListCurrentCoursesAsync(
        long personId,
        string? search,
        string? status,
        CancellationToken cancellationToken);
}
