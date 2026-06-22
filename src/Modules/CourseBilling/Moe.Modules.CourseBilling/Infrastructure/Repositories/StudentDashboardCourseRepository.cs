using Microsoft.EntityFrameworkCore;
using Moe.Modules.CourseBilling.Domain.Courses;
using Moe.Modules.CourseBilling.IGateway.Repositories;
using Moe.Modules.IdentityPlatform.Domain.Schooling;
using Moe.StudentFinance.Persistence;

namespace Moe.Modules.CourseBilling.Infrastructure.Repositories;

internal sealed class StudentDashboardCourseRepository(MoeDbContext dbContext) : IStudentDashboardCourseRepository
{
    public async Task<IReadOnlyCollection<StudentDashboardCourseSummary>> ListCurrentCoursesAsync(
        long personId,
        string? search,
        string? status,
        CancellationToken cancellationToken)
    {
        string? normalizedSearch = Normalize(search);
        string? normalizedStatus = NormalizeStatus(status);

        IQueryable<CourseEnrollment> enrollments = dbContext.Set<CourseEnrollment>()
            .AsNoTracking()
            .Where(x => x.PersonId == personId && x.ExitAtUtc == null);

        if (!string.IsNullOrWhiteSpace(normalizedStatus))
        {
            enrollments = enrollments.Where(x => x.EnrollmentStatusCode == normalizedStatus);
        }

        var query =
            from enrollment in enrollments
            join course in dbContext.Set<Course>().AsNoTracking()
                on enrollment.CourseId equals course.Id
            where string.IsNullOrWhiteSpace(normalizedSearch)
                || course.CourseName.Contains(normalizedSearch)
                || course.CourseCode.Contains(normalizedSearch)
            orderby course.StartDate descending, enrollment.EnrolledAtUtc descending
            select new StudentDashboardCourseSummary(
                enrollment.Id,
                course.Id,
                course.CourseCode,
                course.CourseName,
                LecturerName: null,
                course.StartDate,
                course.EndDate,
                enrollment.EnrollmentStatusCode);

        return await query.ToArrayAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<StudentDashboardCourseSummary>> ListPublishedCoursesAsync(
        long personId,
        string? search,
        CancellationToken cancellationToken)
    {
        string? normalizedSearch = Normalize(search);
        DateOnly today = DateOnly.FromDateTime(DateTime.UtcNow);

        IQueryable<long> activeOrganizationIds = dbContext.Set<SchoolEnrollment>()
            .AsNoTracking()
            .Where(x => x.PersonId == personId
                && x.SchoolingStatusCode == "ACTIVE"
                && x.StartDate <= today
                && (x.EndDate == null || x.EndDate >= today))
            .Select(x => x.OrganizationId);

        IQueryable<long> joinedCourseIds = dbContext.Set<CourseEnrollment>()
            .AsNoTracking()
            .Where(x => x.PersonId == personId && x.ExitAtUtc == null)
            .Select(x => x.CourseId);

        var query =
            from course in dbContext.Set<Course>().AsNoTracking()
            where course.CourseStatusCode == CourseStatusCodes.Published
                && activeOrganizationIds.Contains(course.OrganizationId)
                && !joinedCourseIds.Contains(course.Id)
                && (string.IsNullOrWhiteSpace(normalizedSearch)
                    || course.CourseName.Contains(normalizedSearch)
                    || course.CourseCode.Contains(normalizedSearch))
            orderby course.StartDate descending, course.CourseName
            select new StudentDashboardCourseSummary(
                null,
                course.Id,
                course.CourseCode,
                course.CourseName,
                LecturerName: null,
                course.StartDate,
                course.EndDate,
                "AVAILABLE");

        return await query.ToArrayAsync(cancellationToken);
    }

    private static string? Normalize(string? value)
    {
        string? trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }

    private static string? NormalizeStatus(string? status)
    {
        return Normalize(status)?.ToUpperInvariant() switch
        {
            null => null,
            "ACTIVE" or "IN_PROGRESS" or "INPROGRESS" => CourseEnrollmentStatusCodes.PendingPayment,
            var normalized => normalized
        };
    }
}
