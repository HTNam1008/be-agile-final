using Microsoft.EntityFrameworkCore;
using Moe.Modules.IdentityPlatform.Domain.Iam;
using Moe.Modules.IdentityPlatform.IGateway.Authentication;
using Moe.Modules.IdentityPlatform.IGateway.Repositories;
using Moe.StudentFinance.Persistence;

namespace Moe.Modules.IdentityPlatform.Infrastructure.Repositories;

internal sealed class UserAccountRepository(MoeDbContext dbContext) : IUserAccountRepository, IExternalIdentityProvisioningRepository
{
    public Task<UserAccount?> FindByIdAsync(long userAccountId, CancellationToken cancellationToken)
    {
        return dbContext.Set<UserAccount>()
            .SingleOrDefaultAsync(x => x.Id == userAccountId, cancellationToken);
    }

    public Task<bool> ExistsAdminByEmailAsync(string normalizedEmail, CancellationToken cancellationToken)
    {
        return dbContext.Set<UserAccount>()
            .AnyAsync(x => x.IdentityProviderCode == IdentityProviderCodes.EntraWorkforce
                && x.LoginEmailNormalized == normalizedEmail,
                cancellationToken);
    }

    public Task<bool> ExistsSingpassForPersonAsync(long personId, CancellationToken cancellationToken)
    {
        return dbContext.Set<UserAccount>()
            .AnyAsync(x => x.PersonId == personId
                && x.IdentityProviderCode == IdentityProviderCodes.Singpass,
                cancellationToken);
    }

    public Task<bool> HasActiveExternalIdentityAsync(
        string identityProviderCode,
        string externalIssuer,
        string externalSubjectId,
        CancellationToken cancellationToken)
    {
        return dbContext.Set<UserAccount>()
            .AnyAsync(x => x.IdentityProviderCode == identityProviderCode
                && x.ExternalIssuer == externalIssuer
                && x.ExternalSubjectId == externalSubjectId
                && (x.AccountStatusCode == UserAccountStatusCodes.Active
                    || x.AccountStatusCode == UserAccountStatusCodes.PendingFirstLogin),
                cancellationToken);
    }

    public async Task<UserAccount?> DisableAsync(long userAccountId, DateTime utcNow, CancellationToken cancellationToken)
    {
        UserAccount? account = await FindByIdAsync(userAccountId, cancellationToken);

        if (account is null)
        {
            return null;
        }

        account.Disable(utcNow);
        await dbContext.SaveChangesAsync(cancellationToken);
        return account;
    }

    public async Task<UserAccount?> EnableAsync(long userAccountId, DateTime utcNow, CancellationToken cancellationToken)
    {
        UserAccount? account = await FindByIdAsync(userAccountId, cancellationToken);

        if (account is null)
        {
            return null;
        }

        account.Enable(utcNow);
        await dbContext.SaveChangesAsync(cancellationToken);
        return account;
    }

    public async Task<UserAccount?> UpdateContactDetailsAsync(
        long userAccountId,
        string? contactEmail,
        string? contactMobile,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        UserAccount? account = await FindByIdAsync(userAccountId, cancellationToken);

        if (account is null)
        {
            return null;
        }

        account.UpdateContactDetails(contactEmail, contactMobile, utcNow);
        await dbContext.SaveChangesAsync(cancellationToken);
        return account;
    }
}
