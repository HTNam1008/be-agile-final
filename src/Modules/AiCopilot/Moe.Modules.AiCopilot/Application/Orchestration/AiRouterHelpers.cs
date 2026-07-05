using System.Text.Json;
using Moe.Modules.AiCopilot.Api;

namespace Moe.Modules.AiCopilot.Application.Orchestration;

internal static class AiRouterHelpers
{
    private static readonly string[] AllowedDomains = ["FAS", "PAYMENT", "GENERAL"];
    private static readonly string[] AllowedRoutePrefixes =
    [
        "/portal/account", "/portal/bills", "/portal/courses", "/portal/dashboard",
        "/portal/education-account", "/portal/fas", "/portal/profile"
    ];
    private static readonly HashSet<string> AllowedFieldKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "isWelfareHomeResident", "monthlyHouseholdIncome", "householdMemberCount",
        "parentNationalities", "employmentStatusCode", "otherMonthlyIncome", "email"
    };

    public static AiPageContext? SanitizePageContext(AiPageContext? pageContext)
    {
        if (pageContext is null) return null;
        string domain = AllowedDomains.Contains(pageContext.Domain?.ToUpperInvariant())
            ? pageContext.Domain!.ToUpperInvariant() : "GENERAL";
        string? path = IsAllowedPath(pageContext.Path) ? pageContext.Path : null;
        string? surface = string.IsNullOrWhiteSpace(pageContext.Surface) ? null
            : pageContext.Surface.Length > 80 ? pageContext.Surface[..80] : pageContext.Surface;
        JsonElement? entity = null;
        if (pageContext.Entity.HasValue && domain == "FAS")
        {
            var e = pageContext.Entity.Value;
            if (e.ValueKind == JsonValueKind.Object && e.TryGetProperty("fieldKey", out JsonElement fk) &&
                fk.ValueKind == JsonValueKind.String && fk.GetString() is string fkStr && AllowedFieldKeys.Contains(fkStr))
                entity = e;
        }
        return new AiPageContext(domain, surface, path, entity);
    }

    private static bool IsAllowedPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !path.StartsWith('/')) return false;
        if (path.Contains("..", StringComparison.Ordinal) || path.Contains("://", StringComparison.Ordinal)) return false;
        return AllowedRoutePrefixes.Any(p => path.Equals(p, StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith($"{p}/", StringComparison.OrdinalIgnoreCase));
    }
}
