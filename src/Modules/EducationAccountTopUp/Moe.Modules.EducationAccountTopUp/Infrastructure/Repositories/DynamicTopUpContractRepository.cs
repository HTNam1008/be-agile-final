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

    public async Task<IReadOnlyList<DynamicTopUpContract>> GetByCampaignAndAccountsAsync(long campaignId, IEnumerable<long> accountIds, CancellationToken cancellationToken = default)
    {
        return await dbContext.Set<DynamicTopUpContract>()
            .Where(x => x.TopUpCampaignId == campaignId && accountIds.Contains(x.EducationAccountId))
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

    public async Task<IReadOnlyList<DynamicTopUpContract>> GetByAccountIdAsync(long accountId, CancellationToken cancellationToken = default)
    {
        return await dbContext.Set<DynamicTopUpContract>()
            .AsNoTracking()
            .Where(x => x.EducationAccountId == accountId)
            .ToListAsync(cancellationToken);
    }

    public async Task ShiftContractPaymentDatesAsync(
        long campaignId,
        TimeSpan pauseDuration,
        DateTime nowUtc,
        CancellationToken cancellationToken = default)
    {
        // Bulk-shift NextPaymentDate forward by the exact pause duration.
        // EF Core ExecuteUpdateAsync cannot perform arithmetic on the existing column value
        // (c => c.NextPaymentDate.Value.AddSeconds(n) is not translatable to SQL).
        // FormattableString interpolation produces a fully parameterized query — no SQL injection risk.
        int pauseSeconds = (int)Math.Round(pauseDuration.TotalSeconds);
        if (pauseSeconds <= 0) return;

        await dbContext.Database.ExecuteSqlAsync(
            $"""
             UPDATE topup.DynamicTopUpContract
             SET NextPaymentDate = DATEADD(SECOND, {pauseSeconds}, NextPaymentDate),
                 UpdatedAt       = {nowUtc}
             WHERE TopUpCampaignId = {campaignId}
               AND ContractStatus  = {ContractStatuses.Active}
               AND NextPaymentDate IS NOT NULL
             """,
            cancellationToken);
    }

    public async Task CancelAllActiveContractsAsync(
        long campaignId,
        DateTime cancelledAtUtc,
        CancellationToken cancellationToken = default)
    {
        // When a campaign is cancelled, every ACTIVE contract becomes an orphan —
        // there is no run engine to ever fire them. Flip them to SUSPENDED so the
        // ledger correctly reflects that these students will not receive further payments.
        await dbContext.Set<DynamicTopUpContract>()
            .Where(x => x.TopUpCampaignId == campaignId
                     && x.ContractStatus == ContractStatuses.Active)
            .ExecuteUpdateAsync(s => s
                .SetProperty(c => c.ContractStatus, ContractStatuses.Suspended)
                .SetProperty(c => c.NextPaymentDate, (DateTime?)null)
                .SetProperty(c => c.UpdatedAtUtc, cancelledAtUtc),
            cancellationToken);
    }

    public async Task CancelNonFixedContractActiveContractsAsync(
        long campaignId,
        DateTime completedAtUtc,
        CancellationToken cancellationToken = default)
    {
        // On natural campaign completion (end date reached / schedule exhausted):
        // - INSTANT and CONDITIONAL_RECURRING contracts hard-stop — no future runs will fire them.
        // - FIXED_CONTRACT contracts are intentionally excluded — the product spec guarantees
        //   the student's personal cap promise survives campaign expiry (Pillar 1, Case 2).
        await dbContext.Set<DynamicTopUpContract>()
            .Where(x => x.TopUpCampaignId == campaignId
                     && x.ContractStatus == ContractStatuses.Active
                     && x.DeliveryTypeCode != DeliveryType.FixedContract)
            .ExecuteUpdateAsync(s => s
                .SetProperty(c => c.ContractStatus, ContractStatuses.Suspended)
                .SetProperty(c => c.NextPaymentDate, (DateTime?)null)
                .SetProperty(c => c.UpdatedAtUtc, completedAtUtc),
            cancellationToken);
    }
}
