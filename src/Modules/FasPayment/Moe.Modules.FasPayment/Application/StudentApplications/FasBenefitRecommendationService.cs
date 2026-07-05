using Moe.Modules.FasPayment.Domain.Fas;

namespace Moe.Modules.FasPayment.Application.StudentApplications;

internal sealed class FasBenefitRecommendationService
{
    public const string Matched = "MATCHED";
    public const string NoMatchAdminReview = "NO_MATCH_ADMIN_REVIEW";
    public const string NoOpenScheme = "NO_OPEN_SCHEME";
    public const string NoCriteriaMatch = "NO_CRITERIA_MATCH";

    public FasRecommendationResult Recommend(
        IReadOnlyCollection<FasRecommendationScheme> schemes,
        IReadOnlyCollection<FasTier> tiers,
        IReadOnlyCollection<FasTierCriteriaGroup> groups,
        IReadOnlyCollection<FasTierCriteria> criteria,
        IReadOnlyCollection<FasTierCriteriaNationality> categoricalValues,
        FasRecommendationFacts facts)
    {
        if (schemes.Count == 0)
        {
            return new FasRecommendationResult([], NoOpenScheme, null);
        }

        var matches = new List<FasRecommendationMatch>();
        foreach (FasRecommendationScheme scheme in schemes.OrderBy(x => x.Name).ThenBy(x => x.Id))
        {
            foreach (FasTier tier in tiers.Where(x => x.FasSchemeId == scheme.Id))
            {
                FasTierCriteria[] tierCriteria = criteria
                    .Where(x => x.FasTierId == tier.Id)
                    .OrderBy(x => x.DisplayOrder)
                    .ToArray();

                if (!TierMatches(tier.Id, tierCriteria, groups, categoricalValues, facts))
                {
                    continue;
                }

                matches.Add(new FasRecommendationMatch(
                    scheme.Id,
                    scheme.Name,
                    scheme.Description,
                    tier.Id,
                    tier.Label,
                    tier.SubsidyType,
                    tier.SubsidyValue,
                    tier.DisplayOrder));
            }
        }

        FasRecommendationMatch[] sortedMatches = matches
            .OrderByDescending(x => x.SubsidyValue)
            .ThenBy(x => x.TierDisplayOrder)
            .ThenBy(x => x.SchemeName)
            .ThenBy(x => x.SchemeId)
            .ThenBy(x => x.TierId)
            .ToArray();

        return sortedMatches.Length == 0
            ? new FasRecommendationResult([], NoMatchAdminReview, NoCriteriaMatch)
            : new FasRecommendationResult(sortedMatches, Matched, null);
    }

    private static bool TierMatches(
        long tierId,
        IReadOnlyCollection<FasTierCriteria> tierCriteria,
        IReadOnlyCollection<FasTierCriteriaGroup> allGroups,
        IReadOnlyCollection<FasTierCriteriaNationality> categoricalValues,
        FasRecommendationFacts facts)
    {
        if (tierCriteria.Count == 0)
        {
            return true;
        }

        Dictionary<long, bool> values = tierCriteria.ToDictionary(
            criteria => criteria.Id,
            criteria => CriteriaMatches(criteria, categoricalValues, facts));

        FasTierCriteriaGroup[] groups = allGroups
            .Where(x => x.FasTierId == tierId)
            .OrderBy(x => x.DisplayOrder)
            .ToArray();

        if (groups.Length == 0)
        {
            return LegacyGroups(tierCriteria).Any(group => group.All(criteria => values.GetValueOrDefault(criteria.Id)));
        }

        return groups.Any(group =>
        {
            FasTierCriteria[] groupCriteria = tierCriteria
                .Where(criteria => criteria.FasTierCriteriaGroupId == group.Id)
                .ToArray();
            return groupCriteria.Length > 0 && groupCriteria.All(criteria => values.GetValueOrDefault(criteria.Id));
        });
    }

    private static bool CriteriaMatches(
        FasTierCriteria criteria,
        IReadOnlyCollection<FasTierCriteriaNationality> categoricalValues,
        FasRecommendationFacts facts)
    {
        return criteria.CriteriaType switch
        {
            "AGE" => NumberInRange(facts.Age, criteria),
            "GDP" => NumberInRange(facts.MonthlyHouseholdIncome, criteria),
            "GHI" => true,
            "PCI" => NumberInRange(facts.PerCapitaIncome, criteria),
            "NATIONALITY" => CategoricalMatches(criteria.Id, facts.Nationality, categoricalValues)
                || string.Equals(facts.Nationality, "SG", StringComparison.OrdinalIgnoreCase)
                   && CategoricalMatches(criteria.Id, "Singapore Citizen", categoricalValues),
            "PARENT_NATIONALITY" => facts.ParentNationalities.Any(value => CategoricalMatches(criteria.Id, value, categoricalValues)),
            "ACCOUNT_TYPE" => CategoricalMatches(criteria.Id, facts.AccountType, categoricalValues),
            _ => false
        };
    }

    private static bool NumberInRange(decimal? value, FasTierCriteria criteria)
    {
        if (!value.HasValue)
        {
            return false;
        }

        return (!criteria.NumberFrom.HasValue || value.Value >= criteria.NumberFrom.Value)
            && (!criteria.NumberTo.HasValue || value.Value <= criteria.NumberTo.Value);
    }

    private static bool CategoricalMatches(
        long criteriaId,
        string? value,
        IReadOnlyCollection<FasTierCriteriaNationality> categoricalValues)
    {
        return !string.IsNullOrWhiteSpace(value)
            && categoricalValues.Any(x =>
                x.FasTierCriteriaId == criteriaId &&
                string.Equals(x.Nationality, value, StringComparison.OrdinalIgnoreCase));
    }

    private static IReadOnlyList<IReadOnlyList<FasTierCriteria>> LegacyGroups(IReadOnlyCollection<FasTierCriteria> criteria)
    {
        var groups = new List<IReadOnlyList<FasTierCriteria>>();
        var current = new List<FasTierCriteria>();

        foreach (FasTierCriteria item in criteria.OrderBy(item => item.DisplayOrder))
        {
            current.Add(item);
            if (item.ConnectorToNext != "OR")
            {
                continue;
            }

            groups.Add(current.ToArray());
            current = [];
        }

        if (current.Count > 0)
        {
            groups.Add(current.ToArray());
        }

        return groups;
    }
}

internal sealed record FasRecommendationScheme(long Id, string Name, string? Description);

internal sealed record FasRecommendationFacts(
    decimal? Age,
    decimal? MonthlyHouseholdIncome,
    decimal? PerCapitaIncome,
    string? Nationality,
    IReadOnlyCollection<string> ParentNationalities,
    string? AccountType);

internal sealed record FasRecommendationMatch(
    long SchemeId,
    string SchemeName,
    string? Description,
    long TierId,
    string TierLabel,
    string SubsidyType,
    decimal SubsidyValue,
    int TierDisplayOrder);

internal sealed record FasRecommendationResult(
    IReadOnlyList<FasRecommendationMatch> MatchedSchemes,
    string RecommendationStatus,
    string? ManualReviewReason)
{
    public FasRecommendationMatch? Recommended => MatchedSchemes.FirstOrDefault();
}
