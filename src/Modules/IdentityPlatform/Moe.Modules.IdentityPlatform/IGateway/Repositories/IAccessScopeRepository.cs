using Moe.Modules.IdentityPlatform.Domain.Iam;

namespace Moe.Modules.IdentityPlatform.IGateway.Repositories;

internal interface IAccessScopeRepository
{
    Task<bool> UserAccountCanReceiveAccessAsync(long userAccountId, CancellationToken cancellationToken);

    Task<bool> IsActiveOrganizationUnitAsync(long organizationUnitId, DateTime utcNow, CancellationToken cancellationToken);

    Task<bool> HasActiveRolePermissionsAsync(string roleCode, DateTime utcNow, CancellationToken cancellationToken);

    Task<bool> HasActiveScopeAsync(
        long userAccountId,
        long organizationUnitId,
        string roleCode,
        DateTime utcNow,
        CancellationToken cancellationToken);

    Task AddAsync(UserAccessScope scope, CancellationToken cancellationToken);

    Task<UserAccessScope?> RevokeAsync(long userAccessScopeId, DateTime utcNow, CancellationToken cancellationToken);
}
