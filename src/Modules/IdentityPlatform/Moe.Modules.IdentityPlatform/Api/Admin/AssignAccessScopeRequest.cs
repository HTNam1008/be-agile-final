namespace Moe.Modules.IdentityPlatform.Api.Admin;

public sealed record AssignAccessScopeRequest(
    long OrganizationUnitId,
    string RoleCode,
    DateTime? EffectiveFromUtc);
