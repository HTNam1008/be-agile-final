using Moe.Modules.IdentityPlatform.Domain.Iam;

namespace Moe.Modules.IdentityPlatform.IGateway.Repositories;

internal sealed record CreatedAdminLocalAccount(
    long UserAccountId,
    string AccountStatusCode,
    long InitialScopeId,
    long OrganizationUnitId,
    string RoleCode);

internal interface IAdminUserRepository
{
    Task<bool> IsActiveOrganizationUnitAsync(long organizationUnitId, DateTime utcNow, CancellationToken cancellationToken);

    Task<bool> HasActiveRolePermissionsAsync(string roleCode, DateTime utcNow, CancellationToken cancellationToken);

    Task<CreatedAdminLocalAccount> CreateAdminWithInitialScopeAsync(
        UserAccount account,
        long organizationUnitId,
        string roleCode,
        long actorUserAccountId,
        DateTime utcNow,
        CancellationToken cancellationToken);
}
