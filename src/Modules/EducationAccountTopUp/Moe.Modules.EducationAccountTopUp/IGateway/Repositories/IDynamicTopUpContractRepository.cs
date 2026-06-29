using Moe.Modules.EducationAccountTopUp.Domain.TopUps;

namespace Moe.Modules.EducationAccountTopUp.IGateway.Repositories;

public interface IDynamicTopUpContractRepository
{
    Task<DynamicTopUpContract?> GetByCampaignAndAccountAsync(long campaignId, long accountId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DynamicTopUpContract>> GetActiveByCampaignAsync(long campaignId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DynamicTopUpContract>> GetDueForPaymentAsync(DateTime nowUtc, CancellationToken cancellationToken = default);
    Task AddAsync(DynamicTopUpContract contract, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DynamicTopUpContract>> GetActiveByCampaignAndAccountsAsync(long campaignId, IEnumerable<long> accountIds, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DynamicTopUpContract>> GetByCampaignAndAccountsAsync(long campaignId, IEnumerable<long> accountIds, CancellationToken cancellationToken = default);
    Task SuspendNonQualifyingContractsAsync(long campaignId, IEnumerable<long> qualifyingAccountIds, DateTime suspendedAtUtc, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DynamicTopUpContract>> GetByAccountIdAsync(long accountId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Shifts NextPaymentDate forward by the exact pause duration for all ACTIVE contracts
    /// under a campaign. Called when a paused campaign resumes to restore Mid-Cycle Freeze
    /// invariant: no double-billing, no forfeited cycles.
    /// </summary>
    Task ShiftContractPaymentDatesAsync(long campaignId, TimeSpan pauseDuration, DateTime nowUtc, CancellationToken cancellationToken = default);

    /// <summary>
    /// Terminates all ACTIVE contracts for a cancelled campaign by flipping them to SUSPENDED.
    /// Prevents orphaned contracts from accumulating in the database indefinitely.
    /// </summary>
    Task CancelAllActiveContractsAsync(long campaignId, DateTime cancelledAtUtc, CancellationToken cancellationToken = default);
    /// <summary>
    /// Terminates all ACTIVE non-FixedContract contracts for a cancelled campaign.
    /// Used on natural campaign completion only — FixedContract promises survive expiry per the spec.
    /// </summary>
    Task CancelNonFixedContractActiveContractsAsync(long campaignId, DateTime completedAtUtc, CancellationToken cancellationToken = default);
}

