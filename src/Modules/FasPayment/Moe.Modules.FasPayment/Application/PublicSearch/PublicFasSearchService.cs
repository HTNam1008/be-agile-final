using Microsoft.EntityFrameworkCore;
using Moe.Application.Abstractions.Clock;
using Moe.Modules.CourseBilling.Domain.Courses;
using Moe.Modules.FasPayment.Domain.Fas;
using Moe.Modules.IdentityPlatform.IGateway.Repositories;
using Moe.StudentFinance.Persistence;

namespace Moe.Modules.FasPayment.Application.PublicSearch;

public sealed record PublicFasSchool(long OrganizationId, string OrganizationName);

public sealed record PublicFasSearchRequest(
    long OrganizationId,
    decimal MonthlyHouseholdIncome,
    int HouseholdMemberCount);

public sealed record PublicFasBenefit(string TierLabel, string SubsidyType, decimal SubsidyValue);

public sealed record PublicFasSchemeMatch(
    long SchemeId,
    string Name,
    string? Description,
    DateOnly ApplicationStartDate,
    DateOnly ApplicationEndDate,
    PublicFasBenefit Benefit,
    bool RequiresLoginVerification);

public sealed record PublicFasSearchResult(
    PublicFasSchool School,
    decimal MonthlyHouseholdIncome,
    int HouseholdMemberCount,
    decimal PerCapitaIncome,
    IReadOnlyCollection<PublicFasSchemeMatch> MatchedSchemes);

public sealed class PublicFasSearchService(
    MoeDbContext db,
    IOrganizationUnitRepository organizations,
    IClock clock)
{
    public async Task<IReadOnlyCollection<PublicFasSchool>> ListSchoolsAsync(CancellationToken cancellationToken)
    {
        var units = await organizations.ListActiveAsync(null, cancellationToken);

        return units
            .Where(unit => unit.UnitTypeCode == "SCHOOL")
            .OrderBy(unit => unit.UnitName)
            .Select(unit => new PublicFasSchool(unit.OrganizationUnitId, unit.UnitName))
            .ToArray();
    }

    public async Task<PublicFasSearchResult?> SearchAsync(
        PublicFasSearchRequest request,
        CancellationToken cancellationToken)
    {
        var school = await organizations.FindActiveSchoolByIdAsync(request.OrganizationId, cancellationToken);
        if (school is null)
        {
            return null;
        }

        decimal perCapitaIncome = decimal.Round(
            request.MonthlyHouseholdIncome / request.HouseholdMemberCount,
            2,
            MidpointRounding.AwayFromZero);
        DateOnly today = clock.TodayInSingapore();

        IQueryable<long> schoolCourseIds = db.Set<Course>()
            .AsNoTracking()
            .Where(course => course.OrganizationId == request.OrganizationId)
            .Select(course => course.Id);

        var schemes = await db.Set<FasScheme>()
            .AsNoTracking()
            .Where(scheme =>
                scheme.StatusCode == FasSchemeStatusCodes.Active &&
                scheme.StartDate <= today &&
                scheme.EndDate >= today &&
                (!db.Set<FasSchemeCourse>().Any(link => link.FasSchemeId == scheme.Id) ||
                 db.Set<FasSchemeCourse>().Any(link =>
                     link.FasSchemeId == scheme.Id && schoolCourseIds.Contains(link.CourseId))))
            .OrderBy(scheme => scheme.Name)
            .ToArrayAsync(cancellationToken);

        long[] schemeIds = schemes.Select(scheme => scheme.Id).ToArray();
        var tiers = await db.Set<FasTier>()
            .AsNoTracking()
            .Where(tier => schemeIds.Contains(tier.FasSchemeId))
            .OrderBy(tier => tier.DisplayOrder)
            .ToArrayAsync(cancellationToken);
        long[] tierIds = tiers.Select(tier => tier.Id).ToArray();
        var criteria = await db.Set<FasTierCriteria>()
            .AsNoTracking()
            .Where(item => tierIds.Contains(item.FasTierId))
            .OrderBy(item => item.DisplayOrder)
            .ToArrayAsync(cancellationToken);

        var matches = new List<PublicFasSchemeMatchCandidate>();
        foreach (FasScheme scheme in schemes)
        {
            foreach (FasTier tier in tiers.Where(item => item.FasSchemeId == scheme.Id))
            {
                TierMatch tierMatch = EvaluateTier(
                    criteria.Where(item => item.FasTierId == tier.Id).ToArray(),
                    request.MonthlyHouseholdIncome,
                    perCapitaIncome);
                if (!tierMatch.IsPotentialMatch)
                {
                    continue;
                }

                matches.Add(new PublicFasSchemeMatchCandidate(
                    new PublicFasSchemeMatch(
                        scheme.Id,
                        scheme.Name,
                        scheme.Description,
                        scheme.StartDate,
                        scheme.EndDate,
                        new PublicFasBenefit(tier.Label, tier.SubsidyType, tier.SubsidyValue),
                        tierMatch.RequiresLoginVerification),
                    tier.DisplayOrder));
            }
        }

        PublicFasSchemeMatch[] sortedMatches = matches
            .OrderByDescending(item => item.Match.Benefit.SubsidyValue)
            .ThenBy(item => item.TierDisplayOrder)
            .ThenBy(item => item.Match.Name)
            .ThenBy(item => item.Match.SchemeId)
            .Select(item => item.Match)
            .ToArray();

        return new PublicFasSearchResult(
            new PublicFasSchool(school.OrganizationUnitId, school.UnitName),
            request.MonthlyHouseholdIncome,
            request.HouseholdMemberCount,
            perCapitaIncome,
            sortedMatches);
    }

    private static TierMatch EvaluateTier(
        IReadOnlyList<FasTierCriteria> criteria,
        decimal householdIncome,
        decimal perCapitaIncome)
    {
        if (criteria.Count == 0)
        {
            return new TierMatch(true, false);
        }

        var groups = BuildGroups(criteria);
        TierMatch[] potentialGroups = groups
            .Select(group => EvaluateGroup(group, householdIncome, perCapitaIncome))
            .Where(result => result.IsPotentialMatch)
            .ToArray();

        if (potentialGroups.Length == 0)
        {
            return new TierMatch(false, false);
        }

        return new TierMatch(true, potentialGroups.All(result => result.RequiresLoginVerification));
    }

    private static TierMatch EvaluateGroup(
        IReadOnlyCollection<FasTierCriteria> group,
        decimal householdIncome,
        decimal perCapitaIncome)
    {
        bool requiresLoginVerification = false;
        foreach (FasTierCriteria item in group)
        {
            decimal value;
            switch (item.CriteriaType)
            {
                case "GDP":
                case "GHI":
                    value = householdIncome;
                    break;
                case "PCI":
                    value = perCapitaIncome;
                    break;
                default:
                    requiresLoginVerification = true;
                    continue;
            }

            if (item.NumberFrom.HasValue && value < item.NumberFrom.Value ||
                item.NumberTo.HasValue && value > item.NumberTo.Value)
            {
                return new TierMatch(false, false);
            }
        }

        return new TierMatch(true, requiresLoginVerification);
    }

    private static IReadOnlyCollection<IReadOnlyCollection<FasTierCriteria>> BuildGroups(
        IReadOnlyList<FasTierCriteria> criteria)
    {
        if (criteria.Any(item => item.FasTierCriteriaGroupId > 0))
        {
            return criteria
                .GroupBy(item => item.FasTierCriteriaGroupId)
                .OrderBy(group => group.Key)
                .Select(group => (IReadOnlyCollection<FasTierCriteria>)group.OrderBy(item => item.DisplayOrder).ToArray())
                .ToArray();
        }

        var groups = new List<IReadOnlyCollection<FasTierCriteria>>();
        var current = new List<FasTierCriteria>();
        foreach (FasTierCriteria item in criteria.OrderBy(item => item.DisplayOrder))
        {
            current.Add(item);
            if (item.ConnectorToNext != "OR")
            {
                continue;
            }

            groups.Add(current.ToArray());
            current.Clear();
        }

        if (current.Count > 0)
        {
            groups.Add(current.ToArray());
        }

        return groups;
    }

    private readonly record struct TierMatch(bool IsPotentialMatch, bool RequiresLoginVerification);
    private readonly record struct PublicFasSchemeMatchCandidate(PublicFasSchemeMatch Match, int TierDisplayOrder);
}
