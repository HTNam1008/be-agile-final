namespace Moe.Modules.IdentityPlatform.Application.AdminUsers.CreateAdminUser;

public sealed record CreateAdminUserResponse(
    long UserAccountId,
    string EntraObjectId,
    string Email,
    string DisplayName,
    string AccountStatusCode,
    long InitialAccessScopeId,
    long InitialOrganizationUnitId,
    string AssignedRoleCode,
    long CreatedByUserAccountId);
