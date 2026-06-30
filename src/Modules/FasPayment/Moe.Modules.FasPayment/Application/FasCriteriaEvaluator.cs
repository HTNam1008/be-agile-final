using System.Collections.Generic;
using System.Linq;
using Moe.Modules.FasPayment.Domain.Fas;

namespace Moe.Modules.FasPayment.Application;

internal sealed class FasCriteriaEvaluator
{
    public bool Evaluate(
        IReadOnlyList<FasTierCriteria> criteria,
        ILookup<long, string> nationalitiesByCriteriaId,
        decimal? age,
        decimal? gdpValue,
        decimal? pciValue,
        string? nationality,
        IReadOnlyCollection<string>? parentNationalities = null,
        string? accountType = null)
    {
        if (criteria == null || criteria.Count == 0)
        {
            return true; // No criteria implies eligible
        }

        IReadOnlyList<IReadOnlyList<FasTierCriteria>> groups = BuildGroups(criteria);

        return groups.Any(group => group.All(item =>
            EvaluateSingle(item, nationalitiesByCriteriaId[item.Id], age, gdpValue, pciValue, nationality, parentNationalities, accountType)));
    }

    private bool EvaluateSingle(
        FasTierCriteria c,
        IEnumerable<string> nationalities,
        decimal? age,
        decimal? gdpValue,
        decimal? pciValue,
        string? nationality,
        IReadOnlyCollection<string>? parentNationalities,
        string? accountType)
    {
        switch (c.CriteriaType)
        {
            case "AGE":
                if (!age.HasValue) return false;
                if (c.NumberFrom.HasValue && age.Value < c.NumberFrom.Value) return false;
                if (c.NumberTo.HasValue && age.Value > c.NumberTo.Value) return false;
                return true;

            case "GDP":
                if (!gdpValue.HasValue) return false;
                if (c.NumberFrom.HasValue && gdpValue.Value < c.NumberFrom.Value) return false;
                if (c.NumberTo.HasValue && gdpValue.Value > c.NumberTo.Value) return false;
                return true;

            case "PCI":
                if (!pciValue.HasValue) return false;
                if (c.NumberFrom.HasValue && pciValue.Value < c.NumberFrom.Value) return false;
                if (c.NumberTo.HasValue && pciValue.Value > c.NumberTo.Value) return false;
                return true;

            case "NATIONALITY":
                if (string.IsNullOrWhiteSpace(nationality)) return false;
                return nationalities.Contains(nationality);

            case "PARENT_NATIONALITY":
                return parentNationalities is not null && parentNationalities.Any(nationalities.Contains);

            case "ACCOUNT_TYPE":
                return !string.IsNullOrWhiteSpace(accountType) && nationalities.Contains(accountType);

            default:
                return false;
        }
    }

    private static IReadOnlyList<IReadOnlyList<FasTierCriteria>> BuildGroups(IReadOnlyList<FasTierCriteria> criteria)
    {
        if (criteria.Any(item => item.FasTierCriteriaGroupId > 0))
        {
            return criteria
                .OrderBy(item => item.FasTierCriteriaGroupId)
                .ThenBy(item => item.DisplayOrder)
                .GroupBy(item => item.FasTierCriteriaGroupId)
                .Select(group => (IReadOnlyList<FasTierCriteria>)group.ToArray())
                .ToArray();
        }

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
            current = new List<FasTierCriteria>();
        }

        if (current.Count > 0)
        {
            groups.Add(current.ToArray());
        }

        return groups;
    }
}
