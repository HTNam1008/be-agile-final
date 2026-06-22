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
        string? nationality)
    {
        if (criteria == null || criteria.Count == 0)
        {
            return true; // No criteria implies eligible
        }

        // Left-to-right evaluation without grouping precedence
        bool result = EvaluateSingle(criteria[0], nationalitiesByCriteriaId[criteria[0].Id], age, gdpValue, pciValue, nationality);

        for (int i = 1; i < criteria.Count; i++)
        {
            string? connector = criteria[i - 1].ConnectorToNext;
            bool nextResult = EvaluateSingle(criteria[i], nationalitiesByCriteriaId[criteria[i].Id], age, gdpValue, pciValue, nationality);

            if (connector == "AND")
            {
                result = result && nextResult;
            }
            else if (connector == "OR")
            {
                result = result || nextResult;
            }
            else
            {
                // Default to AND if missing
                result = result && nextResult;
            }
        }

        return result;
    }

    private bool EvaluateSingle(
        FasTierCriteria c,
        IEnumerable<string> nationalities,
        decimal? age,
        decimal? gdpValue,
        decimal? pciValue,
        string? nationality)
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

            default:
                return false;
        }
    }
}
