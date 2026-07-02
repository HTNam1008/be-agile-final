using Microsoft.EntityFrameworkCore;
using Moe.Application.Abstractions.Clock;
using Moe.Modules.IdentityPlatform.Domain.Iam;
using Moe.Modules.IdentityPlatform.Domain.People;
using Moe.Modules.IdentityPlatform.Domain.Schooling;
using Moe.Modules.IdentityPlatform.IGateway.Students;
using Moe.StudentFinance.Persistence;

namespace Moe.Modules.IdentityPlatform.Infrastructure.Students;

internal sealed class StudentDirectory(MoeDbContext dbContext, IClock clock) : IStudentDirectory
{
    public async Task<StudentSummary?> FindByPersonIdAsync(long personId, CancellationToken cancellationToken)
    {
        var student = await (
                from person in dbContext.Set<Person>().AsNoTracking()
                where person.Id == personId
                join schoolEnrollment in dbContext.Set<SchoolEnrollment>().AsNoTracking()
                    on person.Id equals schoolEnrollment.PersonId into schoolEnrollments
                from schoolEnrollment in schoolEnrollments
                    .Where(x => x.SchoolingStatusCode == "ACTIVE")
                    .OrderByDescending(x => x.StartDate)
                    .Take(1)
                    .DefaultIfEmpty()
                join organization in dbContext.Set<OrganizationUnit>().AsNoTracking()
                    on schoolEnrollment.OrganizationId equals organization.Id into organizations
                from organization in organizations.DefaultIfEmpty()
                select new
                {
                    person.Id,
                    person.OfficialFullName,
                    person.DateOfBirth,
                    SchoolName = organization == null ? null : organization.UnitName
                })
            .SingleOrDefaultAsync(cancellationToken);

        return student is null
            ? null
            : new StudentSummary(
                student.Id,
                student.OfficialFullName,
                student.DateOfBirth,
                IsAccountHolder: true,
                student.SchoolName);
    }

    public async Task<IReadOnlyCollection<long>> FindActivePersonIdsByOrganizationAsync(
        long organizationId,
        CancellationToken cancellationToken)
    {
        DateOnly today = DateOnly.FromDateTime(DateTime.UtcNow);

        return await dbContext.Set<SchoolEnrollment>()
            .AsNoTracking()
            .Where(x => x.OrganizationId == organizationId
                && x.SchoolingStatusCode == "ACTIVE"
                && x.StartDate <= today
                && (x.EndDate == null || x.EndDate >= today))
            .Select(x => x.PersonId)
            .Distinct()
            .ToArrayAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AdminStudentSearchSummary>> ListByOrganizationAsync(
        AdminStudentSearchCriteria criteria,
        CancellationToken cancellationToken)
    {
        DateOnly today = DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);
        var query =
            from enrollment in dbContext.Set<SchoolEnrollment>().AsNoTracking()
            join person in dbContext.Set<Person>().AsNoTracking()
                on enrollment.PersonId equals person.Id
            where enrollment.OrganizationId == criteria.OrganizationId
                && enrollment.SchoolingStatusCode == "ACTIVE"
                && enrollment.StartDate <= today
                && (enrollment.EndDate == null || enrollment.EndDate >= today)
            select new
            {
                Enrollment = enrollment,
                Person = person
            };

        if (!string.IsNullOrWhiteSpace(criteria.Search))
        {
            string search = criteria.Search.Trim();
            query = query.Where(x =>
                x.Enrollment.StudentNumber.Contains(search)
                || x.Person.OfficialFullName.Contains(search));
        }

        if (!string.IsNullOrWhiteSpace(criteria.LevelCode))
        {
            string levelCode = criteria.LevelCode.Trim().ToUpperInvariant();
            query = query.Where(x => x.Enrollment.LevelCode == levelCode);
        }

        if (!string.IsNullOrWhiteSpace(criteria.ClassCode))
        {
            string classCode = criteria.ClassCode.Trim().ToUpperInvariant();
            query = query.Where(x => x.Enrollment.ClassCode == classCode);
        }

        return await query
            .OrderBy(x => x.Person.OfficialFullName)
            .ThenBy(x => x.Enrollment.StudentNumber)
            .Skip((criteria.Page - 1) * criteria.PageSize)
            .Take(criteria.PageSize)
            .Select(x => new AdminStudentSearchSummary(
                x.Person.Id,
                x.Enrollment.StudentNumber,
                x.Person.OfficialFullName,
                x.Person.DateOfBirth,
                x.Enrollment.LevelCode,
                x.Enrollment.ClassCode,
                x.Enrollment.SchoolingStatusCode,
                x.Enrollment.OrganizationId))
            .ToArrayAsync(cancellationToken);
    }

    public Task<long> CountByOrganizationAsync(
        AdminStudentSearchCriteria criteria,
        CancellationToken cancellationToken)
    {
        DateOnly today = DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);
        var query =
            from enrollment in dbContext.Set<SchoolEnrollment>().AsNoTracking()
            join person in dbContext.Set<Person>().AsNoTracking()
                on enrollment.PersonId equals person.Id
            where enrollment.OrganizationId == criteria.OrganizationId
                && enrollment.SchoolingStatusCode == "ACTIVE"
                && enrollment.StartDate <= today
                && (enrollment.EndDate == null || enrollment.EndDate >= today)
            select new
            {
                Enrollment = enrollment,
                Person = person
            };

        if (!string.IsNullOrWhiteSpace(criteria.Search))
        {
            string search = criteria.Search.Trim();
            query = query.Where(x =>
                x.Enrollment.StudentNumber.Contains(search)
                || x.Person.OfficialFullName.Contains(search));
        }

        if (!string.IsNullOrWhiteSpace(criteria.LevelCode))
        {
            string levelCode = criteria.LevelCode.Trim().ToUpperInvariant();
            query = query.Where(x => x.Enrollment.LevelCode == levelCode);
        }

        if (!string.IsNullOrWhiteSpace(criteria.ClassCode))
        {
            string classCode = criteria.ClassCode.Trim().ToUpperInvariant();
            query = query.Where(x => x.Enrollment.ClassCode == classCode);
        }

        return query.LongCountAsync(cancellationToken);
    }
}
