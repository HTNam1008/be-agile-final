using Microsoft.EntityFrameworkCore;
using Moe.Modules.CourseBilling.Domain.Courses;
using Moe.Modules.CourseBilling.IGateway.Dashboard;
using Moe.StudentFinance.Persistence;

namespace Moe.Modules.CourseBilling.Infrastructure.Dashboard;

internal sealed class AdminDashboardCourseMetricsReader(MoeDbContext dbContext)
    : IAdminDashboardCourseMetricsReader
{
    public Task<long> CountTotalCoursesAsync(long organizationId, CancellationToken cancellationToken)
        => dbContext.Set<Course>()
            .AsNoTracking()
            .LongCountAsync(course => course.OrganizationId == organizationId, cancellationToken);

}
