namespace Moe.Modules.IdentityPlatform.Infrastructure.Bootstrap;

internal sealed class AdminBootstrapOptions
{
    public const string SectionName = "AdminBootstrap";
    public bool Enabled { get; init; }
    public string EntraObjectId { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public long OrganizationUnitId { get; init; } = 1;
    public SchoolAdminBootstrapOptions[] SchoolAdmins { get; init; } = [];
}

internal sealed class SchoolAdminBootstrapOptions
{
    public bool Enabled { get; init; }
    public string EntraObjectId { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public long OrganizationUnitId { get; init; }
}
