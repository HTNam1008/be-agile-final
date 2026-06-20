using Microsoft.EntityFrameworkCore;
using Moe.Modules.CourseBilling.Domain.Courses;
using Moe.Modules.CourseBilling.IGateway.Repositories;
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
