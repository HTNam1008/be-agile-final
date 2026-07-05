using Microsoft.EntityFrameworkCore;
using Moe.Modules.IdentityPlatform.Domain.Iam;
using Moe.Modules.Mfa.Application.RecoverPin;
using Moe.StudentFinance.Persistence;

namespace Moe.Modules.IdentityPlatform.Infrastructure.Authentication;

internal sealed class MfaRecoveryContactResolver(MoeDbContext dbContext) : IMfaRecoveryContactResolver
{
    public Task<string?> ResolveEmailAsync(long loginAccountId, CancellationToken cancellationToken) =>
        dbContext.Set<UserAccount>().AsNoTracking().Where(x => x.Id == loginAccountId)
            .Select(x => x.ContactEmail ?? x.ProviderEmail ?? x.LoginEmailNormalized)
            .SingleOrDefaultAsync(cancellationToken);
}
