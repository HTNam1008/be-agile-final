namespace Moe.Modules.CourseBilling.IGateway.Dashboard;

internal interface IAdminDashboardCourseMetricsReader
{
    Task<long> CountActiveCoursesAsync(
        long? organizationId,
        DateOnly currentDate,
        CancellationToken cancellationToken);

    Task<long> CountActiveEnrollmentsAsync(
        long organizationId,
        DateOnly currentDate,
        CancellationToken cancellationToken);
}
