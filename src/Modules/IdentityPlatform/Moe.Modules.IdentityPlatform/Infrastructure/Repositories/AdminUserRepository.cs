using Microsoft.EntityFrameworkCore;
using Moe.Modules.IdentityPlatform.Domain.Iam;
using Moe.Modules.IdentityPlatform.IGateway.Repositories;
using Moe.StudentFinance.Persistence;

namespace Moe.Modules.IdentityPlatform.Infrastructure.Repositories;

internal sealed class AdminUserRepository(MoeDbContext dbContext) : IAdminUserRepository
{
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

    public async Task<CreatedAdminLocalAccount> CreateAdminWithInitialScopeAsync(
        UserAccount account,
        long organizationUnitId,
        string roleCode,
        long actorUserAccountId,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        var executionStrategy = dbContext.Database.CreateExecutionStrategy();

        return await executionStrategy.ExecuteAsync(async () =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

            dbContext.Add(account);
            await dbContext.SaveChangesAsync(cancellationToken);

            UserAccessScope initialScope = new(
                account.Id,
                organizationUnitId,
                roleCode,
                actorUserAccountId,
                utcNow,
                utcNow);

            dbContext.Add(initialScope);
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return new CreatedAdminLocalAccount(
                account.Id,
                account.AccountStatusCode,
                initialScope.Id,
                initialScope.OrganizationUnitId,
                initialScope.RoleCode);
        });
    }
}
