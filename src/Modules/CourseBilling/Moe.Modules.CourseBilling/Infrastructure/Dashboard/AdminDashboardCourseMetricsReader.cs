using Microsoft.EntityFrameworkCore;
using Moe.Modules.CourseBilling.Domain.Courses;
using Moe.Modules.CourseBilling.IGateway.Dashboard;
using Moe.StudentFinance.Persistence;

namespace Moe.Modules.CourseBilling.Infrastructure.Dashboard;

internal sealed class AdminDashboardCourseMetricsReader(MoeDbContext dbContext)
    : IAdminDashboardCourseMetricsReader
{
    private static readonly string[] ActiveEnrollmentStatuses =
    [
        CourseEnrollmentStatusCodes.Active,
        CourseEnrollmentStatusCodes.PaidInFull,
        CourseEnrollmentStatusCodes.PaymentPastDue
    ];

    public Task<long> CountActiveCoursesAsync(
        long? organizationId,
        DateOnly currentDate,
        CancellationToken cancellationToken)
        => ActiveCourses(organizationId, currentDate).LongCountAsync(cancellationToken);

    public Task<long> CountActiveEnrollmentsAsync(
        long organizationId,
        DateOnly currentDate,
        CancellationToken cancellationToken)
        => (
            from enrollment in dbContext.Set<CourseEnrollment>().AsNoTracking()
            join course in ActiveCourses(organizationId, currentDate)
                on enrollment.CourseId equals course.Id
            where ActiveEnrollmentStatuses.Contains(enrollment.EnrollmentStatusCode)
            select enrollment.Id)
            .LongCountAsync(cancellationToken);

    private IQueryable<Course> ActiveCourses(long? organizationId, DateOnly currentDate)
        => dbContext.Set<Course>()
            .AsNoTracking()
            .Where(course => (organizationId == null || course.OrganizationId == organizationId)
                && course.CourseStatusCode == CourseStatusCodes.Published
                && course.StartDate <= currentDate
                && course.EndDate >= currentDate);
}
