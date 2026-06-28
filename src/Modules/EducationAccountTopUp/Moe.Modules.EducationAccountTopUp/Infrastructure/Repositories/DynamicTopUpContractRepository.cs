using Microsoft.EntityFrameworkCore;
using Moe.Modules.EducationAccountTopUp.Domain.TopUps;
using Moe.Modules.EducationAccountTopUp.IGateway.Repositories;
using Moe.StudentFinance.Persistence;

namespace Moe.Modules.EducationAccountTopUp.Infrastructure.Repositories;

internal sealed class DynamicTopUpContractRepository(MoeDbContext dbContext) : IDynamicTopUpContractRepository
{
    public Task<DynamicTopUpContract?> GetByCampaignAndAccountAsync(long campaignId, long accountId, CancellationToken cancellationToken = default)
    {
        return dbContext.Set<DynamicTopUpContract>()
            .SingleOrDefaultAsync(x => x.TopUpCampaignId == campaignId && x.EducationAccountId == accountId, cancellationToken);
    }

    public async Task<IReadOnlyList<DynamicTopUpContract>> GetActiveByCampaignAsync(long campaignId, CancellationToken cancellationToken = default)
    {
        return await dbContext.Set<DynamicTopUpContract>()
            .Where(x => x.TopUpCampaignId == campaignId && x.ContractStatus == ContractStatuses.Active)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<DynamicTopUpContract>> GetDueForPaymentAsync(DateTime nowUtc, CancellationToken cancellationToken = default)
    {
        return await dbContext.Set<DynamicTopUpContract>()
            .Where(x => x.ContractStatus == ContractStatuses.Active
                && x.NextPaymentDate != null
                && x.NextPaymentDate <= nowUtc
                && x.TotalReceived < x.MaxTotalAmount)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(DynamicTopUpContract contract, CancellationToken cancellationToken = default)
    {
        await dbContext.Set<DynamicTopUpContract>().AddAsync(contract, cancellationToken);
    }

    public async Task<IReadOnlyList<DynamicTopUpContract>> GetActiveByCampaignAndAccountsAsync(long campaignId, IEnumerable<long> accountIds, CancellationToken cancellationToken = default)
    {
        return await dbContext.Set<DynamicTopUpContract>()
            .Where(x => x.TopUpCampaignId == campaignId && x.ContractStatus == ContractStatuses.Active && accountIds.Contains(x.EducationAccountId))
            .ToListAsync(cancellationToken);
    }

    public async Task SuspendNonQualifyingContractsAsync(long campaignId, IEnumerable<long> qualifyingAccountIds, DateTime suspendedAtUtc, CancellationToken cancellationToken = default)
    {
        // Bulk update is supported in EF Core 7+
        await dbContext.Set<DynamicTopUpContract>()
            .Where(x => x.TopUpCampaignId == campaignId 
                     && x.ContractStatus == ContractStatuses.Active
                     && x.DeliveryTypeCode == DeliveryType.ConditionalRecurring
                     && !qualifyingAccountIds.Contains(x.EducationAccountId))
            .ExecuteUpdateAsync(s => s
                .SetProperty(c => c.ContractStatus, ContractStatuses.Suspended)
                .SetProperty(c => c.NextPaymentDate, (DateTime?)null)
                .SetProperty(c => c.UpdatedAtUtc, suspendedAtUtc), cancellationToken);
    }
}
