using Microsoft.EntityFrameworkCore;
using Moe.Infrastructure.Shared.Api;
using Moe.Modules.CourseBilling.Domain.Billing;
using Moe.Modules.CourseBilling.Domain.Courses;
using Moe.Modules.CourseBilling.IGateway.AdminStudentCourses;
using Moe.StudentFinance.Persistence;

namespace Moe.Modules.CourseBilling.Infrastructure.AdminStudentCourses;

internal sealed class AdminStudentEnrolledCourseReader(MoeDbContext dbContext) : IAdminStudentEnrolledCourseReader
{
    public async Task<PageResponse<AdminStudentEnrolledCourseProjection>> ListAsync(
        long personId,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        int safePage = Math.Max(1, page);
        int safePageSize = Math.Clamp(pageSize, 1, 100);

        IQueryable<CourseEnrollment> enrollmentQuery = dbContext.Set<CourseEnrollment>()
            .AsNoTracking()
            .Where(x => x.PersonId == personId);

        long totalCount = await enrollmentQuery.LongCountAsync(cancellationToken);

        AdminStudentEnrolledCourseProjection[] items = await (
                from enrollment in enrollmentQuery
                join course in dbContext.Set<Course>().AsNoTracking()
                    on enrollment.CourseId equals course.Id
                join bill in dbContext.Set<Bill>().AsNoTracking()
                    on enrollment.Id equals bill.CourseEnrollmentId into bills
                from bill in bills
                    .OrderByDescending(x => x.IssuedAtUtc)
                    .ThenByDescending(x => x.Id)
                    .Take(1)
                    .DefaultIfEmpty()
                orderby enrollment.EnrolledAtUtc descending, enrollment.Id descending
                select new AdminStudentEnrolledCourseProjection(
                    course.Id,
                    course.CourseName,
                    enrollment.EnrollmentStatusCode,
                    enrollment.EnrolledAtUtc,
                    bill == null ? 0m : bill.GrossAmount,
                    bill == null ? 0m : bill.SubsidyAmount,
                    bill == null ? 0m : bill.PaidAmount,
                    bill == null ? 0m : bill.OutstandingAmount))
            .Skip((safePage - 1) * safePageSize)
            .Take(safePageSize)
            .ToArrayAsync(cancellationToken);

        return new PageResponse<AdminStudentEnrolledCourseProjection>(
            items,
            safePage,
            safePageSize,
            totalCount);
    }
}
