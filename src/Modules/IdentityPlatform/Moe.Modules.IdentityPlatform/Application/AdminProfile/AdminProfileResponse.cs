namespace Moe.Modules.IdentityPlatform.Application.AdminProfile;

public sealed record AdminProfileResponse(
    long UserAccountId,
    string DisplayName,
    string? LoginEmail,
    string? ContactEmail,
    string? ContactMobile,
    string RoleCode,
    long? AdminOrganizationId,
    string IdentityProviderCode,
    string? ExternalTenantId,
    string ExternalIssuer,
    string ExternalSubjectId,
    string? ExternalObjectId,
    string? ProviderDisplayName,
    string? ProviderLoginName,
    string? ProviderEmail,
    string AccountStatusCode,
    DateTime? FirstLoginAtUtc,
    DateTime? LastLoginAtUtc,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    IReadOnlyCollection<long> OrganizationUnitIds,
    IReadOnlyCollection<string> Roles,
    IReadOnlyCollection<string> Permissions,
    AdminProfileOrganizationResponse? Organization);

public sealed record AdminProfileOrganizationResponse(
    long Id,
    string Code,
    string Name,
    string TypeCode);
