using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moe.Modules.IdentityPlatform.Domain.Iam;
using Moe.Modules.IdentityPlatform.Infrastructure.EntraWorkforce;
using Moe.StudentFinance.Persistence;

namespace Moe.Modules.IdentityPlatform.Infrastructure.Bootstrap;

internal sealed class AdminBootstrapHostedService(
    IServiceScopeFactory scopeFactory,
    IOptions<AdminBootstrapOptions> bootstrapOptions,
    IOptions<EntraWorkforceDirectoryOptions> entraOptions,
    ILogger<AdminBootstrapHostedService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        AdminBootstrapOptions bootstrap = bootstrapOptions.Value;

        if (!bootstrap.Enabled)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(bootstrap.EntraObjectId) || string.IsNullOrWhiteSpace(bootstrap.Email))
        {
            logger.LogWarning("Admin bootstrap is enabled but EntraObjectId or Email is missing.");
            return;
        }

        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
        MoeDbContext dbContext = scope.ServiceProvider.GetRequiredService<MoeDbContext>();
        DateTime utcNow = DateTime.UtcNow;
        string normalizedEmail = bootstrap.Email.Trim().ToUpperInvariant();
        string entraObjectId = bootstrap.EntraObjectId.Trim();

        bool organizationUnitExists = await dbContext.Set<OrganizationUnit>()
            .AnyAsync(x => x.Id == bootstrap.OrganizationUnitId
                && x.StatusCode == IamStatusCodes.Active
                && x.EffectiveFromUtc <= utcNow
                && (x.EffectiveToUtc == null || x.EffectiveToUtc > utcNow),
                cancellationToken);

        bool adminRoleExists = await dbContext.Set<RolePermission>()
            .AnyAsync(x => x.RoleCode == RoleCodes.SystemAdmin
                && x.StatusCode == IamStatusCodes.Active
                && x.EffectiveFromUtc <= utcNow
                && (x.EffectiveToUtc == null || x.EffectiveToUtc > utcNow),
                cancellationToken);

        if (!organizationUnitExists || !adminRoleExists)
        {
            logger.LogWarning("Admin bootstrap skipped because IAM seed data is missing. Apply database migrations first.");
            return;
        }

        UserAccount? account = await dbContext.Set<UserAccount>()
            .SingleOrDefaultAsync(x => x.IdentityProviderCode == IdentityProviderCodes.EntraWorkforce
                && x.ExternalObjectId == entraObjectId,
                cancellationToken);

        if (account is null)
        {
            EntraWorkforceDirectoryOptions entra = entraOptions.Value;
            account = UserAccount.CreateBootstrapAdmin(
                entra.EffectiveIssuer,
                entraObjectId,
                entra.TenantId,
                entraObjectId,
                normalizedEmail,
                string.IsNullOrWhiteSpace(bootstrap.DisplayName) ? bootstrap.Email.Trim() : bootstrap.DisplayName.Trim(),
                utcNow);

            dbContext.Add(account);
            await dbContext.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Bootstrapped local admin user account for {Email}.", bootstrap.Email);
        }

        bool activeAdminScopeExists = await dbContext.Set<UserAccessScope>()
            .AnyAsync(x => x.UserAccountId == account.Id
                && x.OrganizationUnitId == bootstrap.OrganizationUnitId
                && x.RoleCode == RoleCodes.SystemAdmin
                && x.StatusCode == IamStatusCodes.Active
                && x.EffectiveFromUtc <= utcNow
                && (x.EffectiveToUtc == null || x.EffectiveToUtc > utcNow),
                cancellationToken);

        if (activeAdminScopeExists)
        {
            return;
        }

        UserAccessScope adminAccessScope = new(
            account.Id,
            bootstrap.OrganizationUnitId,
            RoleCodes.SystemAdmin,
            account.Id,
            utcNow,
            utcNow);

        dbContext.Add(adminAccessScope);
        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Bootstrapped ADMIN access scope for {Email}.", bootstrap.Email);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
