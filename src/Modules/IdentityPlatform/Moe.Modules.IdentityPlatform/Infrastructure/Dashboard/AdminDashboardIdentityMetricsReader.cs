using Microsoft.EntityFrameworkCore;
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
        DateTime yearStart = new(year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        DateTime yearEnd = yearStart.AddYears(1);
        DateTime monthStart = new(utcNow.Year, utcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        DateTime monthEnd = monthStart.AddMonths(1);

        IQueryable<SchoolEnrollment> scopedEnrollments = dbContext.Set<SchoolEnrollment>()
            .AsNoTracking()
            .Where(enrollment => organizationId == null || enrollment.OrganizationId == organizationId);

        IQueryable<long> activeStudentIds =
            from enrollment in scopedEnrollments
            join person in dbContext.Set<Person>().AsNoTracking()
                on enrollment.PersonId equals person.Id
            where enrollment.SchoolingStatusCode == "ACTIVE"
                && person.PersonStatusCode == PersonStatusCodes.Active
            select person.Id;

        long totalActiveStudents = await activeStudentIds
            .Distinct()
            .LongCountAsync(cancellationToken);

        long totalActiveSchools = organizationId == null
            ? await dbContext.Set<OrganizationUnit>()
                .AsNoTracking()
                .Where(organization => organization.UnitTypeCode == "SCHOOL"
                    && organization.StatusCode == "ACTIVE"
                    && organization.EffectiveFromUtc <= utcNow
                    && (organization.EffectiveToUtc == null || organization.EffectiveToUtc > utcNow))
                .LongCountAsync(cancellationToken)
            : 0;

        long newStudentsThisMonth = await (
            from enrollment in scopedEnrollments
            join person in dbContext.Set<Person>().AsNoTracking()
                on enrollment.PersonId equals person.Id
            where enrollment.CreatedAtUtc >= monthStart
                && enrollment.CreatedAtUtc < monthEnd
                && person.PersonStatusCode == PersonStatusCodes.Active
            select enrollment.PersonId)
            .Distinct()
            .LongCountAsync(cancellationToken);

        IReadOnlyCollection<AdminDashboardCountPoint> monthlyNewStudents = [];
        if (organizationId == null)
        {
            var monthlyRows = await (
                from enrollment in scopedEnrollments
                join person in dbContext.Set<Person>().AsNoTracking()
                    on enrollment.PersonId equals person.Id
                where enrollment.CreatedAtUtc >= yearStart
                    && enrollment.CreatedAtUtc < yearEnd
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

        return new AdminDashboardIdentityMetrics(
            totalActiveSchools,
            totalActiveStudents,
            newStudentsThisMonth,
            monthlyNewStudents);
    }
}
