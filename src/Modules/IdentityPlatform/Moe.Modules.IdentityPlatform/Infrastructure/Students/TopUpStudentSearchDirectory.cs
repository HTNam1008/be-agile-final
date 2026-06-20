using Microsoft.EntityFrameworkCore;
using Moe.Application.Abstractions.Clock;
using Moe.Modules.IdentityPlatform.Domain.People;
using Moe.Modules.IdentityPlatform.Domain.Schooling;
using Moe.Modules.IdentityPlatform.IGateway.Students;
using Moe.Modules.IdentityPlatform.IGateway.Students.TopUpSearch;
using Moe.StudentFinance.Persistence;

namespace Moe.Modules.IdentityPlatform.Infrastructure.Students;

internal sealed class TopUpStudentSearchDirectory(MoeDbContext dbContext, IClock clock) : ITopUpStudentSearchDirectory
{
    public async Task<TopUpStudentSearchSummaryPage> SearchForTopUpAsync(
        TopUpStudentSearchCriteria criteria,
        IReadOnlyCollection<long> scopedOrganizationIds,
        CancellationToken cancellationToken)
    {
        IQueryable<TopUpStudentSearchSummary> query = BuildTopUpSearchQuery(criteria, scopedOrganizationIds);

        long total = await query.LongCountAsync(cancellationToken);

        TopUpStudentSearchSummary[] items = await query
            .Skip((criteria.Page - 1) * criteria.PageSize)
            .Take(criteria.PageSize)
            .ToArrayAsync(cancellationToken);

        return new TopUpStudentSearchSummaryPage(
            items,
            criteria.Page,
            criteria.PageSize,
            total);
    }

    public async Task<IReadOnlyCollection<long>> FindMatchingPersonIdsForTopUpAsync(
        TopUpStudentSearchCriteria criteria,
        IReadOnlyCollection<long> scopedOrganizationIds,
        CancellationToken cancellationToken)
    {
        IQueryable<TopUpStudentSearchSummary> query = BuildTopUpSearchQuery(criteria, scopedOrganizationIds);

        return await query
            .Select(x => x.PersonId)
            .Distinct()
            .ToArrayAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<long, TopUpStudentDisplaySummary>> FindDisplayByPersonIdsForTopUpAsync(
        IReadOnlyCollection<long> personIds,
        long organizationId,
        CancellationToken cancellationToken)
    {
        if (personIds.Count == 0)
        {
            return new Dictionary<long, TopUpStudentDisplaySummary>();
        }

        TopUpStudentDisplaySummary[] rows = await (
            from enrollment in dbContext.Set<SchoolEnrollment>().AsNoTracking()
            join person in dbContext.Set<Person>().AsNoTracking()
                on enrollment.PersonId equals person.Id
            where personIds.Contains(enrollment.PersonId)
            orderby enrollment.PersonId,
                enrollment.OrganizationId == organizationId descending,
                enrollment.StartDate descending,
                enrollment.Id descending
            select new TopUpStudentDisplaySummary(
                enrollment.PersonId,
                enrollment.StudentNumber,
                person.OfficialFullName))
            .ToArrayAsync(cancellationToken);

        return rows
            .DistinctBy(x => x.PersonId)
            .ToDictionary(x => x.PersonId);
    }

    private IQueryable<TopUpStudentSearchSummary> BuildTopUpSearchQuery(
        TopUpStudentSearchCriteria criteria,
        IReadOnlyCollection<long> scopedOrganizationIds)
    {
        var query =
            from enrollment in dbContext.Set<SchoolEnrollment>().AsNoTracking()
            join person in dbContext.Set<Person>().AsNoTracking()
                on enrollment.PersonId equals person.Id
            select new
            {
                Person = person,
                Enrollment = enrollment
            };

        if (scopedOrganizationIds.Count > 0)
        {
            query = query.Where(x => scopedOrganizationIds.Contains(x.Enrollment.OrganizationId));
        }

        if (criteria.CandidatePersonIds is not null)
        {
            query = criteria.CandidatePersonIds.Count == 0
                ? query.Where(_ => false)
                : query.Where(x => criteria.CandidatePersonIds.Contains(x.Person.Id));
        }

        if (criteria.OrganizationId.HasValue)
        {
            query = query.Where(x => x.Enrollment.OrganizationId == criteria.OrganizationId.Value);
        }

        if (!string.IsNullOrWhiteSpace(criteria.Search))
        {
            string search = criteria.Search.Trim();
            query = criteria.AccountSearchPersonIds is { Count: > 0 }
                ? query.Where(x => x.Enrollment.StudentNumber.Contains(search)
                    || x.Person.OfficialFullName.Contains(search)
                    || criteria.AccountSearchPersonIds.Contains(x.Person.Id))
                : query.Where(x => x.Enrollment.StudentNumber.Contains(search) || x.Person.OfficialFullName.Contains(search));
        }

        if (!string.IsNullOrWhiteSpace(criteria.SchoolingStatusCode))
        {
            string schoolingStatusCode = criteria.SchoolingStatusCode.Trim();
            query = query.Where(x => x.Enrollment.SchoolingStatusCode == schoolingStatusCode);
        }

        if (!string.IsNullOrWhiteSpace(criteria.LevelCode))
        {
            string levelCode = criteria.LevelCode.Trim();
            query = query.Where(x => x.Enrollment.LevelCode == levelCode);
        }

        if (!string.IsNullOrWhiteSpace(criteria.ClassCode))
        {
            string classCode = criteria.ClassCode.Trim();
            query = query.Where(x => x.Enrollment.ClassCode == classCode);
        }

        DateOnly today = DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);

        if (criteria.AgeFrom.HasValue)
        {
            DateOnly latestBirthDate = today.AddYears(-criteria.AgeFrom.Value);
            query = query.Where(x => x.Person.DateOfBirth <= latestBirthDate);
        }

        if (criteria.AgeTo.HasValue)
        {
            DateOnly earliestBirthDate = today.AddYears(-(criteria.AgeTo.Value + 1)).AddDays(1);
            query = query.Where(x => x.Person.DateOfBirth >= earliestBirthDate);
        }

        return query
            .OrderBy(x => x.Person.OfficialFullName)
            .ThenBy(x => x.Enrollment.StudentNumber)
            .ThenBy(x => x.Person.Id)
            .Select(x => new TopUpStudentSearchSummary(
                x.Person.Id,
                x.Enrollment.StudentNumber,
                x.Person.OfficialFullName,
                x.Person.DateOfBirth,
                x.Enrollment.SchoolingStatusCode,
                x.Enrollment.LevelCode,
                x.Enrollment.ClassCode,
                x.Enrollment.OrganizationId));
    }
}
