using System.ComponentModel.DataAnnotations;

namespace Moe.Infrastructure.Shared.Configuration;

public sealed class PortalOptions
{
    public const string SectionName = "Portals";
    public string[] AdminAllowedOrigins { get; init; } = [];
    public string[] EServiceAllowedOrigins { get; init; } = [];
}

public sealed class JwtSchemeOptions
{
    [Required] public string Authority { get; init; } = string.Empty;
    [Required] public string Audience { get; init; } = string.Empty;
    public string ClientId { get; init; } = string.Empty;
    public string[] Scopes { get; init; } = [];
    public string? AllowedTenantId { get; init; }
    public bool RequireHttpsMetadata { get; init; } = true;
    public string LocalTokenSigningKey { get; init; } = string.Empty;
    public int LocalTokenLifetimeMinutes { get; init; } = 30;
}

public sealed class SingpassSchemeOptions
{
    [Required] public string Authority { get; init; } = string.Empty;
    [Required] public string Audience { get; init; } = string.Empty;
    public string? AllowedTenantId { get; init; }
    public bool RequireHttpsMetadata { get; init; } = true;
    public string Mode { get; init; } = "MockPass";
    public string DiscoveryEndpoint { get; init; } = string.Empty;
    public string ClientId { get; init; } = string.Empty;
    public string[] Scopes { get; init; } = [];
    public string RedirectUri { get; init; } = string.Empty;
    public string PortalRedirectUri { get; init; } = string.Empty;
    public string LocalTokenSigningKey { get; init; } = string.Empty;
    public int LocalTokenLifetimeMinutes { get; init; } = 30;
    public string MockPassRpPrivateJwksPath { get; init; } = string.Empty;
}

public sealed class AuthenticationOptions
{
    public const string SectionName = "Authentication";
    [Required] public JwtSchemeOptions AdminEntra { get; init; } = new();
    [Required] public SingpassSchemeOptions EServiceSingpass { get; init; } = new();
}

public sealed class UatOptions
{
    public const string SectionName = "UAT";
    public bool EnableMockIntegrations { get; init; }
    public bool EnableSwagger { get; init; }
}
