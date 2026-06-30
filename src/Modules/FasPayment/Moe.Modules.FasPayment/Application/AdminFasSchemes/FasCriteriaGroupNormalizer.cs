using Moe.Modules.FasPayment.Contracts.AdminFasSchemes;

namespace Moe.Modules.FasPayment.Application.AdminFasSchemes;

internal static class FasCriteriaGroupNormalizer
{
    public static IReadOnlyList<FasCriteriaGroupRequest> Normalize(CreateFasSchemeRequest request)
    {
        if (request.CriteriaGroups is { Count: > 0 })
        {
            return request.CriteriaGroups
                .OrderBy(group => group.DisplayOrder)
                .Select(group => group with { Criteria = group.Criteria.OrderBy(criteria => criteria.DisplayOrder).ToArray() })
                .ToArray();
        }

        return FromLegacyTemplate(request.CriteriaTemplate);
    }

    public static IReadOnlyList<FasCriteriaTemplateItem> Flatten(IReadOnlyList<FasCriteriaGroupRequest> groups)
        => groups
            .OrderBy(group => group.DisplayOrder)
            .SelectMany((group, groupIndex) =>
            {
                FasCriteriaTemplateItem[] criteria = group.Criteria.OrderBy(item => item.DisplayOrder).ToArray();
                return criteria.Select((item, index) =>
                {
                    bool lastInGroup = index == criteria.Length - 1;
                    bool lastGroup = groupIndex == groups.Count - 1;
                    string? connector = lastInGroup
                        ? lastGroup ? null : "OR"
                        : "AND";

                    return item with { ConnectorToNext = connector };
                });
            })
            .ToArray();

    private static IReadOnlyList<FasCriteriaGroupRequest> FromLegacyTemplate(IReadOnlyList<FasCriteriaTemplateItem>? template)
    {
        if (template is null || template.Count == 0)
        {
            return Array.Empty<FasCriteriaGroupRequest>();
        }

        var groups = new List<FasCriteriaGroupRequest>();
        var current = new List<FasCriteriaTemplateItem>();
        int groupOrder = 1;

        foreach (FasCriteriaTemplateItem item in template.OrderBy(item => item.DisplayOrder))
        {
            current.Add(item with { ConnectorToNext = null });
            if (item.ConnectorToNext != "OR")
            {
                continue;
            }

            groups.Add(new FasCriteriaGroupRequest(groupOrder++, current.ToArray()));
            current = new List<FasCriteriaTemplateItem>();
        }

        if (current.Count > 0)
        {
            groups.Add(new FasCriteriaGroupRequest(groupOrder, current.ToArray()));
        }

        return groups;
    }
}
