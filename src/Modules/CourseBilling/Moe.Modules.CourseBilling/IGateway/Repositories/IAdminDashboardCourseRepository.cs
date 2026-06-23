namespace Moe.Modules.CourseBilling.IGateway.Repositories;

internal interface IAdminDashboardCourseRepository
{
    Task<long> CountCoursesAsync(long? organizationId, CancellationToken cancellationToken);
}
