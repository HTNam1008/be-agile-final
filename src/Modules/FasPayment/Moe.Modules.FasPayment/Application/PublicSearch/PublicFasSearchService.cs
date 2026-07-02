using Microsoft.EntityFrameworkCore;
using Moe.Application.Abstractions.Clock;
using Moe.Modules.CourseBilling.Domain.Courses;
using Moe.Modules.FasPayment.Domain.Fas;
using Moe.Modules.IdentityPlatform.IGateway.Repositories;
using Moe.StudentFinance.Persistence;

namespace Moe.Modules.FasPayment.Application.PublicSearch;

public sealed record PublicFasSearchRequest(
    long OrganizationId,
    decimal MonthlyHouseholdIncome,
    int HouseholdMemberCount,
    string? ParentNationality);

public sealed record PublicSchoolItem(long OrganizationId, string OrganizationCode, string OrganizationName);
public sealed record PublicFasBenefit(string TierLabel, string SubsidyType, decimal SubsidyValue);
public sealed record PublicFasSchemeResult(
    long SchemeId,
    string Name,
    string? Description,
    DateOnly ApplicationStartDate,
    DateOnly ApplicationEndDate,
    PublicFasBenefit Benefit,
    bool RequiresLoginVerification);
public sealed record PublicFasSearchResult(
    PublicSchoolItem School,
    decimal MonthlyHouseholdIncome,
    int HouseholdMemberCount,
    decimal PerCapitaIncome,
    IReadOnlyCollection<PublicFasSchemeResult> MatchedSchemes);

public sealed class PublicFasSearchService(
    MoeDbContext db,
    IOrganizationUnitRepository organizations,
    IClock clock)
{
    private const int MaximumResults = 50;

    public async Task<IReadOnlyCollection<PublicSchoolItem>> ListSchools(CancellationToken cancellationToken)
    {
        var rows = await organizations.ListActiveAsync(null, cancellationToken);
        return rows
            .Where(x => string.Equals(x.UnitTypeCode, "SCHOOL", StringComparison.OrdinalIgnoreCase))
            .OrderBy(x => x.UnitName)
            .Select(x => new PublicSchoolItem(x.OrganizationUnitId, x.UnitCode, x.UnitName))
            .ToArray();
    }

    public async Task<PublicFasSearchResult?> Search(PublicFasSearchRequest request, CancellationToken cancellationToken)
    {
        if (request.OrganizationId <= 0 || request.MonthlyHouseholdIncome < 0 || request.HouseholdMemberCount <= 0)
            throw new ArgumentException("FAS.INVALID_PUBLIC_SEARCH");

        var school = await organizations.FindActiveSchoolByIdAsync(request.OrganizationId, cancellationToken);
        if (school is null) return null;

        decimal perCapitaIncome = decimal.Round(request.MonthlyHouseholdIncome / request.HouseholdMemberCount, 2);
        DateOnly today = SingaporeBusinessDay.FromUtc(clock.UtcNow.UtcDateTime);
        IQueryable<long> schoolCourseIds = db.Set<Course>().AsNoTracking()
            .Where(x => x.OrganizationId == request.OrganizationId)
            .Select(x => x.Id);

        var schemes = await db.Set<FasScheme>().AsNoTracking()
            .Where(scheme => scheme.StatusCode == FasSchemeStatusCodes.Active
                && scheme.StartDate <= today
                && scheme.EndDate >= today
                && (!db.Set<FasSchemeCourse>().Any(link => link.FasSchemeId == scheme.Id)
                    || db.Set<FasSchemeCourse>().Any(link => link.FasSchemeId == scheme.Id
                        && schoolCourseIds.Contains(link.CourseId))))
            .OrderBy(x => x.EndDate)
            .ThenBy(x => x.Name)
            .Take(MaximumResults)
            .ToArrayAsync(cancellationToken);

        var matches = new List<PublicFasSchemeResult>();
        foreach (FasScheme scheme in schemes)
        {
            PublicFasSchemeResult? match = await MatchScheme(
                scheme,
                request.MonthlyHouseholdIncome,
                perCapitaIncome,
                request.ParentNationality,
                cancellationToken);
            if (match is not null) matches.Add(match);
        }

        return new PublicFasSearchResult(
            new PublicSchoolItem(school.OrganizationUnitId, school.UnitCode, school.UnitName),
            request.MonthlyHouseholdIncome,
            request.HouseholdMemberCount,
            perCapitaIncome,
            matches);
    }

    private async Task<PublicFasSchemeResult?> MatchScheme(
        FasScheme scheme,
        decimal income,
        decimal pci,
        string? parentNationality,
        CancellationToken cancellationToken)
    {
        FasTier[] tiers = await db.Set<FasTier>().AsNoTracking()
            .Where(x => x.FasSchemeId == scheme.Id)
            .OrderBy(x => x.DisplayOrder)
            .ToArrayAsync(cancellationToken);

        foreach (FasTier tier in tiers)
        {
            FasTierCriteria[] criteria = await db.Set<FasTierCriteria>().AsNoTracking()
                .Where(x => x.FasTierId == tier.Id)
                .OrderBy(x => x.DisplayOrder)
                .ToArrayAsync(cancellationToken);
            FasTierCriteriaGroup[] groups = await db.Set<FasTierCriteriaGroup>().AsNoTracking()
                .Where(x => x.FasTierId == tier.Id)
                .OrderBy(x => x.DisplayOrder)
                .ToArrayAsync(cancellationToken);

            var evaluations = new Dictionary<long, CriterionEvaluation>();
            foreach (FasTierCriteria criterion in criteria)
            {
                evaluations[criterion.Id] = await EvaluateCriterion(
                    criterion, income, pci, parentNationality, cancellationToken);
            }

            IReadOnlyList<IReadOnlyList<FasTierCriteria>> alternatives = BuildAlternatives(groups, criteria);
            IReadOnlyList<FasTierCriteria>? possible = alternatives.FirstOrDefault(group =>
                group.Count > 0 && group.All(item => evaluations[item.Id] != CriterionEvaluation.Failed));
            if (criteria.Length == 0) possible = Array.Empty<FasTierCriteria>();
            if (possible is null) continue;

            bool requiresVerification = possible.Any(item => evaluations[item.Id] == CriterionEvaluation.Unknown);
            return new PublicFasSchemeResult(
                scheme.Id,
                scheme.Name,
                scheme.Description,
                scheme.StartDate,
                scheme.EndDate,
                new PublicFasBenefit(tier.Label, tier.SubsidyType, tier.SubsidyValue),
                requiresVerification);
        }

        return null;
    }

    private async Task<CriterionEvaluation> EvaluateCriterion(
        FasTierCriteria criterion,
        decimal income,
        decimal pci,
        string? parentNationality,
        CancellationToken cancellationToken)
    {
        if (criterion.CriteriaType is "GDP" or "GHI")
            return InRange(income, criterion) ? CriterionEvaluation.Passed : CriterionEvaluation.Failed;
        if (criterion.CriteriaType == "PCI")
            return InRange(pci, criterion) ? CriterionEvaluation.Passed : CriterionEvaluation.Failed;
        if (criterion.CriteriaType != "PARENT_NATIONALITY" || string.IsNullOrWhiteSpace(parentNationality))
            return CriterionEvaluation.Unknown;

        bool matched = await db.Set<FasTierCriteriaNationality>().AsNoTracking()
            .AnyAsync(x => x.FasTierCriteriaId == criterion.Id
                && x.Nationality.ToUpper() == parentNationality.Trim().ToUpper(), cancellationToken);
        return matched ? CriterionEvaluation.Passed : CriterionEvaluation.Failed;
    }

    private static bool InRange(decimal value, FasTierCriteria criterion)
        => (!criterion.NumberFrom.HasValue || value >= criterion.NumberFrom.Value)
            && (!criterion.NumberTo.HasValue || value <= criterion.NumberTo.Value);

    private static IReadOnlyList<IReadOnlyList<FasTierCriteria>> BuildAlternatives(
        IReadOnlyCollection<FasTierCriteriaGroup> groups,
        IReadOnlyCollection<FasTierCriteria> criteria)
    {
        if (groups.Count > 0)
            return groups.OrderBy(x => x.DisplayOrder)
                .Select(group => (IReadOnlyList<FasTierCriteria>)criteria
                    .Where(x => x.FasTierCriteriaGroupId == group.Id)
                    .OrderBy(x => x.DisplayOrder)
                    .ToArray())
                .ToArray();

        var alternatives = new List<IReadOnlyList<FasTierCriteria>>();
        var current = new List<FasTierCriteria>();
        foreach (FasTierCriteria item in criteria.OrderBy(x => x.DisplayOrder))
        {
            current.Add(item);
            if (item.ConnectorToNext == "OR")
            {
                alternatives.Add(current.ToArray());
                current.Clear();
            }
        }
        if (current.Count > 0) alternatives.Add(current.ToArray());
        return alternatives;
    }

    private enum CriterionEvaluation { Failed, Passed, Unknown }
}
