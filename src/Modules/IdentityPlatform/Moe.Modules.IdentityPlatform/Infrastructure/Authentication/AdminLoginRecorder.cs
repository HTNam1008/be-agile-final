using Microsoft.EntityFrameworkCore;
using Moe.Modules.IdentityPlatform.Domain.Iam;
using Moe.Modules.IdentityPlatform.IGateway.Authentication;
using Moe.StudentFinance.Persistence;

namespace Moe.Modules.IdentityPlatform.Infrastructure.Authentication;

internal sealed class AdminLoginRecorder(MoeDbContext dbContext) : IAdminLoginRecorder
{
    public async Task<bool> RecordSuccessfulLoginAsync(
        long userAccountId,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        UserAccount? account = await dbContext.Set<UserAccount>()
            .SingleOrDefaultAsync(
                x => x.Id == userAccountId && x.PortalAccessCode == PortalAccessCodes.Admin,
                cancellationToken);

        if (account is null || !account.IsActiveForLogin)
        {
            return false;
        }

        account.RecordSuccessfulLogin(utcNow);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}
