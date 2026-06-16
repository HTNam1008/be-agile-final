using Microsoft.EntityFrameworkCore;
using Moe.Modules.IdentityPlatform.Domain.Iam;
using Moe.Modules.IdentityPlatform.IGateway.Repositories;
using Moe.StudentFinance.Persistence;

namespace Moe.Modules.IdentityPlatform.Infrastructure.Repositories;

internal sealed class AccessScopeRepository(MoeDbContext dbContext) : IAccessScopeRepository
{
    public Task<bool> UserAccountCanReceiveAccessAsync(long userAccountId, CancellationToken cancellationToken)
    {
        return dbContext.Set<UserAccount>()
            .AnyAsync(x => x.Id == userAccountId
                && x.AccountStatusCode != UserAccountStatusCodes.Disabled,
                cancellationToken);
    }

    public Task<bool> IsActiveOrganizationUnitAsync(long organizationUnitId, DateTime utcNow, CancellationToken cancellationToken)
    {
        return dbContext.Set<OrganizationUnit>()
            .AnyAsync(x => x.Id == organizationUnitId
                && x.StatusCode == IamStatusCodes.Active
                && x.EffectiveFromUtc <= utcNow
                && (x.EffectiveToUtc == null || x.EffectiveToUtc > utcNow),
                cancellationToken);
    }

    public Task<bool> HasActiveRolePermissionsAsync(string roleCode, DateTime utcNow, CancellationToken cancellationToken)
    {
        return dbContext.Set<RolePermission>()
            .AnyAsync(x => x.RoleCode == roleCode
                && x.StatusCode == IamStatusCodes.Active
                && x.EffectiveFromUtc <= utcNow
                && (x.EffectiveToUtc == null || x.EffectiveToUtc > utcNow),
                cancellationToken);
    }

    public Task<bool> HasActiveScopeAsync(
        long userAccountId,
        long organizationUnitId,
        string roleCode,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        return dbContext.Set<UserAccessScope>()
            .AnyAsync(x => x.UserAccountId == userAccountId
                && x.OrganizationUnitId == organizationUnitId
                && x.RoleCode == roleCode
                && x.StatusCode == IamStatusCodes.Active
                && x.EffectiveFromUtc <= utcNow
                && (x.EffectiveToUtc == null || x.EffectiveToUtc > utcNow),
                cancellationToken);
    }

    public async Task AddAsync(UserAccessScope scope, CancellationToken cancellationToken)
    {
        await dbContext.Set<UserAccessScope>().AddAsync(scope, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<UserAccessScope?> RevokeAsync(long userAccessScopeId, DateTime utcNow, CancellationToken cancellationToken)
    {
        UserAccessScope? scope = await dbContext.Set<UserAccessScope>()
            .SingleOrDefaultAsync(x => x.Id == userAccessScopeId, cancellationToken);

        if (scope is null)
        {
            return null;
        }

        scope.Revoke(utcNow);
        await dbContext.SaveChangesAsync(cancellationToken);
        return scope;
    }
}
