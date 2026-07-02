using Microsoft.EntityFrameworkCore;
using Moe.Application.Abstractions.Clock;
using Moe.Modules.IdentityPlatform.Domain.Iam;
using Moe.Modules.IdentityPlatform.Domain.People;
using Moe.Modules.IdentityPlatform.Domain.Schooling;
using Moe.Modules.IdentityPlatform.IGateway.Dashboard;
using Moe.StudentFinance.Persistence;

namespace Moe.Modules.IdentityPlatform.Infrastructure.Dashboard;

internal sealed class AdminDashboardIdentityMetricsReader(MoeDbContext dbContext)
    : IAdminDashboardIdentityMetricsReader
{
    public Task<AdminDashboardIdentityMetrics> GetHqMetricsAsync(
        int year,
        DateTimeOffset now,
        CancellationToken cancellationToken)
        => GetMetricsAsync(null, year, now, cancellationToken);

    public Task<AdminDashboardIdentityMetrics> GetSchoolMetricsAsync(
        long organizationId,
        int year,
        DateTimeOffset now,
        CancellationToken cancellationToken)
        => GetMetricsAsync(organizationId, year, now, cancellationToken);

    private async Task<AdminDashboardIdentityMetrics> GetMetricsAsync(
        long? organizationId,
        int year,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        DateTime utcNow = now.UtcDateTime;
        DateTimeOffset singaporeNow = now.ToUniversalTime().ToOffset(TimeSpan.FromHours(8));
        DateTime previousPeriodCutoffUtc = PreviousPeriodCutoff(singaporeNow).UtcDateTime;
        DateOnly currentDate = SingaporeBusinessDay.FromUtc(now);

        IQueryable<SchoolEnrollment> scopedEnrollments = dbContext.Set<SchoolEnrollment>()
            .AsNoTracking()
            .Where(enrollment => organizationId == null || enrollment.OrganizationId == organizationId);

        long totalStudents = await CountTotalStudentsAtAsync(scopedEnrollments, utcNow, cancellationToken);
        long previousPeriodTotalStudents = await CountTotalStudentsAtAsync(
            scopedEnrollments,
            previousPeriodCutoffUtc,
            cancellationToken);

        long totalSchools = 0;
        long previousPeriodTotalSchools = 0;
        if (organizationId == null)
        {
            totalSchools = await CountTotalSchoolsAtAsync(utcNow, cancellationToken);
            previousPeriodTotalSchools = await CountTotalSchoolsAtAsync(previousPeriodCutoffUtc, cancellationToken);
        }

        IReadOnlyCollection<AdminDashboardCountPoint> monthlyNewStudents = [];
        IReadOnlyCollection<AdminDashboardNullableCountPoint> monthlyActiveStudents = [];
        if (organizationId == null)
        {
            DateTime yearStartUtc = new(year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            DateTime yearEndUtc = yearStartUtc.AddYears(1);
            var monthlyRows = await (
                from enrollment in scopedEnrollments
                join person in dbContext.Set<Person>().AsNoTracking()
                    on enrollment.PersonId equals person.Id
                where enrollment.CreatedAtUtc >= yearStartUtc
                    && enrollment.CreatedAtUtc < yearEndUtc
                    && person.PersonStatusCode == PersonStatusCodes.Active
                select new { enrollment.PersonId, Month = enrollment.CreatedAtUtc.Month })
                .Distinct()
                .GroupBy(item => item.Month)
                .Select(group => new { Month = group.Key, Value = group.LongCount() })
                .ToListAsync(cancellationToken);
            monthlyNewStudents = monthlyRows
                .Select(row => new AdminDashboardCountPoint(row.Month, row.Value))
                .ToArray();
        }
        else
        {
            DateOnly yearStart = new(year, 1, 1);
            DateOnly yearEnd = new(year, 12, 31);
            ActiveEnrollmentRow[] enrollmentRows = await (
                from enrollment in scopedEnrollments
                join person in dbContext.Set<Person>().AsNoTracking()
                    on enrollment.PersonId equals person.Id
                where enrollment.SchoolingStatusCode == "ACTIVE"
                    && person.PersonStatusCode == PersonStatusCodes.Active
                    && enrollment.StartDate <= yearEnd
                    && (enrollment.EndDate == null || enrollment.EndDate >= yearStart)
                select new ActiveEnrollmentRow(
                    enrollment.PersonId,
                    enrollment.StartDate,
                    enrollment.EndDate))
                .ToArrayAsync(cancellationToken);

            monthlyActiveStudents = Enumerable.Range(1, 12)
                .Select(month => BuildMonthlyActiveStudentPoint(year, month, currentDate, enrollmentRows))
                .ToArray();
        }

        return new AdminDashboardIdentityMetrics(
            totalSchools,
            previousPeriodTotalSchools,
            totalStudents,
            previousPeriodTotalStudents,
            monthlyNewStudents,
            monthlyActiveStudents);
    }

    private Task<long> CountTotalSchoolsAtAsync(DateTime cutoffUtc, CancellationToken cancellationToken)
        => dbContext.Set<OrganizationUnit>()
            .AsNoTracking()
            .Where(organization => organization.UnitTypeCode == "SCHOOL"
                && organization.CreatedAtUtc <= cutoffUtc)
            .LongCountAsync(cancellationToken);

    private static Task<long> CountTotalStudentsAtAsync(
        IQueryable<SchoolEnrollment> enrollments,
        DateTime cutoffUtc,
        CancellationToken cancellationToken)
        => enrollments
            .Where(enrollment => enrollment.CreatedAtUtc <= cutoffUtc)
            .Select(enrollment => enrollment.PersonId)
            .Distinct()
            .LongCountAsync(cancellationToken);

    private static DateTimeOffset PreviousPeriodCutoff(DateTimeOffset singaporeNow)
    {
        DateTimeOffset previousMonth = singaporeNow.AddMonths(-1);
        int day = Math.Min(singaporeNow.Day, DateTime.DaysInMonth(previousMonth.Year, previousMonth.Month));
        DateTime localCutoff = new(previousMonth.Year, previousMonth.Month, day);
        localCutoff = localCutoff.Add(singaporeNow.TimeOfDay);
        return new DateTimeOffset(localCutoff, singaporeNow.Offset).ToUniversalTime();
    }

    private static AdminDashboardNullableCountPoint BuildMonthlyActiveStudentPoint(
        int year,
        int month,
        DateOnly currentDate,
        IReadOnlyCollection<ActiveEnrollmentRow> rows)
    {
        DateOnly monthStart = new(year, month, 1);
        if (monthStart > currentDate)
        {
            return new AdminDashboardNullableCountPoint(month, null);
        }

        DateOnly monthEnd = new(year, month, DateTime.DaysInMonth(year, month));
        DateOnly snapshot = monthEnd > currentDate ? currentDate : monthEnd;
        long value = rows
            .Where(row => row.StartDate <= snapshot && (row.EndDate == null || row.EndDate >= snapshot))
            .Select(row => row.PersonId)
            .Distinct()
            .LongCount();

        return new AdminDashboardNullableCountPoint(month, value);
    }

    private sealed record ActiveEnrollmentRow(long PersonId, DateOnly StartDate, DateOnly? EndDate);
}
