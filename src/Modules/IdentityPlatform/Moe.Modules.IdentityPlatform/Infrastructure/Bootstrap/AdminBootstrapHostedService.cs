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
        try
        {
            await BootstrapAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Admin bootstrap failed during application startup. The application will continue to start; apply migrations and verify database access, then restart the app.");
        }
    }

    private async Task BootstrapAsync(CancellationToken cancellationToken)
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
            .AnyAsync(x => x.RoleCode == RoleCodes.HqAdmin
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
                && x.RoleCode == RoleCodes.HqAdmin
                && x.StatusCode == IamStatusCodes.Active
                && x.EffectiveFromUtc <= utcNow
                && (x.EffectiveToUtc == null || x.EffectiveToUtc > utcNow),
                cancellationToken);

        if (!activeAdminScopeExists)
        {
            UserAccessScope adminAccessScope = new(
                account.Id,
                bootstrap.OrganizationUnitId,
                RoleCodes.HqAdmin,
                account.Id,
                utcNow,
                utcNow);

            dbContext.Add(adminAccessScope);
            await dbContext.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Bootstrapped ADMIN access scope for {Email}.", bootstrap.Email);
        }

        await BootstrapSchoolAdminsAsync(dbContext, account, bootstrap.SchoolAdmins, entraOptions.Value, cancellationToken);
    }

    private async Task BootstrapSchoolAdminsAsync(
        MoeDbContext dbContext,
        UserAccount hqAdmin,
        IEnumerable<SchoolAdminBootstrapOptions> schoolAdmins,
        EntraWorkforceDirectoryOptions entra,
        CancellationToken cancellationToken)
    {
        foreach (SchoolAdminBootstrapOptions schoolAdmin in schoolAdmins.Where(x => x.Enabled))
        {
            if (string.IsNullOrWhiteSpace(schoolAdmin.EntraObjectId)
                || string.IsNullOrWhiteSpace(schoolAdmin.Email)
                || schoolAdmin.OrganizationUnitId <= 0)
            {
                logger.LogWarning("School admin bootstrap entry is enabled but EntraObjectId, Email, or OrganizationUnitId is missing.");
                continue;
            }

            DateTime utcNow = DateTime.UtcNow;
            bool schoolExists = await dbContext.Set<OrganizationUnit>()
                .AnyAsync(x => x.Id == schoolAdmin.OrganizationUnitId
                    && x.UnitTypeCode == "SCHOOL"
                    && x.StatusCode == IamStatusCodes.Active
                    && x.EffectiveFromUtc <= utcNow
                    && (x.EffectiveToUtc == null || x.EffectiveToUtc > utcNow),
                    cancellationToken);

            if (!schoolExists)
            {
                logger.LogWarning("School admin bootstrap skipped for {Email}: organization unit {OrganizationUnitId} is not an active school.", schoolAdmin.Email, schoolAdmin.OrganizationUnitId);
                continue;
            }

            string objectId = schoolAdmin.EntraObjectId.Trim();
            UserAccount? account = await dbContext.Set<UserAccount>()
                .SingleOrDefaultAsync(x => x.IdentityProviderCode == IdentityProviderCodes.EntraWorkforce
                    && x.ExternalObjectId == objectId,
                    cancellationToken);

            if (account is null)
            {
                account = UserAccount.CreateAdmin(
                    entra.EffectiveIssuer,
                    objectId,
                    entra.TenantId,
                    objectId,
                    schoolAdmin.Email.Trim().ToUpperInvariant(),
                    string.IsNullOrWhiteSpace(schoolAdmin.DisplayName) ? schoolAdmin.Email.Trim() : schoolAdmin.DisplayName.Trim(),
                    RoleCodes.SchoolAdmin,
                    schoolAdmin.OrganizationUnitId,
                    hqAdmin.Id,
                    utcNow);

                dbContext.Add(account);
                await dbContext.SaveChangesAsync(cancellationToken);
                logger.LogInformation("Bootstrapped local school admin account for {Email}.", schoolAdmin.Email);
            }

            bool activeScopeExists = await dbContext.Set<UserAccessScope>()
                .AnyAsync(x => x.UserAccountId == account.Id
                    && x.OrganizationUnitId == schoolAdmin.OrganizationUnitId
                    && x.RoleCode == RoleCodes.SchoolAdmin
                    && x.StatusCode == IamStatusCodes.Active
                    && x.EffectiveFromUtc <= utcNow
                    && (x.EffectiveToUtc == null || x.EffectiveToUtc > utcNow),
                    cancellationToken);

            if (activeScopeExists)
            {
                continue;
            }

            dbContext.Add(new UserAccessScope(account.Id, schoolAdmin.OrganizationUnitId, RoleCodes.SchoolAdmin, hqAdmin.Id, utcNow, utcNow));
            await dbContext.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Bootstrapped SCHOOL_ADMIN access scope for {Email} at organization unit {OrganizationUnitId}.", schoolAdmin.Email, schoolAdmin.OrganizationUnitId);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
