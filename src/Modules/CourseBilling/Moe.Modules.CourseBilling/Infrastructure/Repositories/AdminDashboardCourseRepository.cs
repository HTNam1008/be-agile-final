using Microsoft.EntityFrameworkCore;
using Moe.Modules.CourseBilling.Domain.Courses;
using Moe.Modules.CourseBilling.IGateway.Repositories;
using Moe.StudentFinance.Persistence;

namespace Moe.Modules.CourseBilling.Infrastructure.Repositories;

internal sealed class AdminDashboardCourseRepository(MoeDbContext dbContext) : IAdminDashboardCourseRepository
{
    public Task<long> CountCoursesAsync(long? organizationId, CancellationToken cancellationToken)
        => dbContext.Set<Course>()
            .AsNoTracking()
            .Where(course => (organizationId == null || course.OrganizationId == organizationId)
                && course.CourseStatusCode != CourseStatusCodes.Disabled)
            .LongCountAsync(cancellationToken);
}
