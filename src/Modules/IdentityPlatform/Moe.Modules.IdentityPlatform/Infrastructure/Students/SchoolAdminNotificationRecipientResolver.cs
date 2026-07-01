using Microsoft.EntityFrameworkCore;
using Moe.Modules.IdentityPlatform.Domain.Iam;
using Moe.Modules.IdentityPlatform.IGateway.Students;
using Moe.StudentFinance.Persistence;

namespace Moe.Modules.IdentityPlatform.Infrastructure.Students;

internal sealed class SchoolAdminNotificationRecipientResolver(MoeDbContext dbContext) : ISchoolAdminNotificationRecipientResolver
{
    public async Task<IReadOnlyCollection<long>> FindUserAccountIdsByOrganizationIdAsync(long organizationId, CancellationToken cancellationToken)
    {
        return await dbContext.Set<UserAccount>()
            .AsNoTracking()
            .Where(x => x.AdminOrganizationId == organizationId
                && x.PortalAccessCode == PortalAccessCodes.Admin
                && x.RoleCode == RoleCodes.SchoolAdmin
                && (x.AccountStatusCode == UserAccountStatusCodes.Active || x.AccountStatusCode == UserAccountStatusCodes.PendingFirstLogin))
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);
    }
}
