using System.Text.RegularExpressions;
using Moe.Modules.FasPayment.Contracts.AdminFasSchemes;

namespace Moe.Modules.FasPayment.Application.AdminFasSchemes;

internal static partial class FasSchemeRequestDefaults
{
    public static CreateFasSchemeRequest WithSystemGrantCode(CreateFasSchemeRequest request)
    {
        string schemeCode = request.SchemeCode?.Trim() ?? string.Empty;
        string safeSchemeCode = GrantCodeUnsafeCharacters().Replace(schemeCode.ToUpperInvariant(), "-").Trim('-');
        string grantCode = $"GRANT-{(string.IsNullOrWhiteSpace(safeSchemeCode) ? "FAS" : safeSchemeCode)}";

        return request with
        {
            SchemeCode = schemeCode,
            GrantCode = grantCode
        };
    }

    [GeneratedRegex("[^A-Z0-9]+")]
    private static partial Regex GrantCodeUnsafeCharacters();
}
