namespace Moe.Modules.IdentityPlatform.Application.Organizations;

public sealed record OrganizationUnitSummary(
    long OrganizationUnitId,
    long? ParentOrganizationUnitId,
    string UnitCode,
    string UnitName,
    string UnitTypeCode,
    string StatusCode);
