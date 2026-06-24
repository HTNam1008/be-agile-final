namespace Moe.Modules.CourseBilling.IGateway.Repositories;

internal interface IStudentDashboardCourseRepository
{
    Task<int> CountCurrentCoursesAsync(
        long personId,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<StudentDashboardCourseSummary>> ListCurrentCoursesAsync(
        long personId,
        string? search,
        string? status,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<StudentDashboardCourseSummary>> ListPublishedCoursesAsync(
        long personId,
        string? search,
        CancellationToken cancellationToken);
}
