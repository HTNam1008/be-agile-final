namespace Moe.Modules.IdentityPlatform.Infrastructure.EntraWorkforce;

public sealed class EntraWorkforceDirectoryOptions
{
    public const string SectionName = "EntraWorkforceDirectory";

    public string TenantId { get; init; } = string.Empty;
    public string ClientId { get; init; } = string.Empty;
    public string ClientSecret { get; init; } = string.Empty;
    public string GraphBaseUrl { get; init; } = "https://graph.microsoft.com/v1.0";
    public string? Issuer { get; init; }

    public string EffectiveIssuer => string.IsNullOrWhiteSpace(Issuer)
        ? $"https://login.microsoftonline.com/{TenantId}/v2.0"
        : Issuer;
}
