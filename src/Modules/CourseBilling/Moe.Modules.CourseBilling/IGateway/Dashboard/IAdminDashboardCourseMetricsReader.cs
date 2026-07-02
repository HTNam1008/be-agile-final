namespace Moe.Modules.CourseBilling.IGateway.Dashboard;

internal interface IAdminDashboardCourseMetricsReader
{
    Task<long> CountTotalCoursesAsync(long organizationId, CancellationToken cancellationToken);
}
