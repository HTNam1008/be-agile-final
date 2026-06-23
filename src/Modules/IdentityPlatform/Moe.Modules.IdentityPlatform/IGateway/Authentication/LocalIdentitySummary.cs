namespace Moe.Modules.IdentityPlatform.IGateway.Authentication;

public sealed record LocalIdentitySummary(
    long UserAccountId,
    long? PersonId,
    string DisplayName,
    string IdentityProviderCode,
    string PortalAccessCode,
    string AccountStatusCode,
    int? Age,
    bool IsAccountHolder,
    IReadOnlyCollection<long> OrganizationUnitIds,
    IReadOnlyCollection<string> Roles,
    IReadOnlyCollection<string> Permissions);
