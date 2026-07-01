using Microsoft.EntityFrameworkCore;
using Moe.Modules.EducationAccountTopUp.Domain.EducationAccounts;
using Moe.Modules.EducationAccountTopUp.IGateway.Repositories;
using Moe.StudentFinance.Persistence;

namespace Moe.Modules.EducationAccountTopUp.Infrastructure.Repositories;

internal sealed class AccountHoldRepository(MoeDbContext dbContext) : IAccountHoldRepository
{
    public Task<bool> HasPendingHoldAsync(
        long educationAccountId,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        return dbContext.Set<AccountHold>()
            .AsNoTracking()
            .AnyAsync(
                hold => hold.EducationAccountId == educationAccountId
                    && hold.HoldStatusCode == AccountHoldStatusCodes.Reserved
                    && hold.ExpiresAtUtc > utcNow,
                cancellationToken);
    }
}
