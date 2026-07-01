using Microsoft.EntityFrameworkCore;
using Moe.Modules.EducationAccountTopUp.Domain.EducationAccounts;
using Moe.Modules.EducationAccountTopUp.IGateway.Repositories;
using Moe.StudentFinance.Persistence;

namespace Moe.Modules.EducationAccountTopUp.Infrastructure.Repositories;

internal sealed class SettlementPreferenceRepository(MoeDbContext dbContext) : ISettlementPreferenceRepository
{
    public Task<SettlementPreference?> FindActiveByEducationAccountIdAsync(
        long educationAccountId,
        CancellationToken cancellationToken)
    {
        return dbContext.Set<SettlementPreference>()
            .SingleOrDefaultAsync(
                preference => preference.EducationAccountId == educationAccountId && preference.IsActive,
                cancellationToken);
    }

    public async Task AddAsync(SettlementPreference preference, CancellationToken cancellationToken)
    {
        await dbContext.Set<SettlementPreference>().AddAsync(preference, cancellationToken);
    }
}
