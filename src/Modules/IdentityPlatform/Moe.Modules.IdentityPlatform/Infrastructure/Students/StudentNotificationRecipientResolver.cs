using Microsoft.EntityFrameworkCore;
using Moe.Modules.IdentityPlatform.Domain.Iam;
using Moe.Modules.IdentityPlatform.IGateway.Students;
using Moe.StudentFinance.Persistence;

namespace Moe.Modules.IdentityPlatform.Infrastructure.Students;

internal sealed class StudentNotificationRecipientResolver(MoeDbContext dbContext) : IStudentNotificationRecipientResolver
{
    public async Task<long?> FindUserAccountIdByPersonIdAsync(long personId, CancellationToken cancellationToken)
    {
        return await dbContext.Set<UserAccount>()
            .AsNoTracking()
            .Where(x => x.PersonId == personId && x.PortalAccessCode == PortalAccessCodes.EService)
            .OrderByDescending(x => x.AccountStatusCode == UserAccountStatusCodes.Active)
            .ThenByDescending(x => x.AccountStatusCode == UserAccountStatusCodes.PendingFirstLogin)
            .ThenByDescending(x => x.LastLoginAtUtc)
            .ThenByDescending(x => x.UpdatedAtUtc)
            .ThenByDescending(x => x.Id)
            .Select(x => (long?)x.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
