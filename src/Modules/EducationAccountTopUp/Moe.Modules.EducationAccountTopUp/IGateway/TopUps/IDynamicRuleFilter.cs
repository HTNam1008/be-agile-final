using Moe.Modules.EducationAccountTopUp.Application.TopUps.GetCampaigns;

namespace Moe.Modules.EducationAccountTopUp.IGateway.TopUps;

/// <summary>
/// Applies dynamic campaign rules to filter eligible education account IDs.
/// Implementation lives in Infrastructure and is the ONLY place that may reference EF Core query composition.
/// The Application layer passes Application-layer projections (CampaignRuleProjection); Infrastructure maps internally.
/// </summary>
public interface IDynamicRuleFilter
{
    /// <summary>
    /// Returns a paged list of education account IDs that match all active rules.
    /// </summary>
    Task<IReadOnlyList<long>> FilterAccountIdsAsync(
        IReadOnlyList<CampaignRuleProjection> rules,
        int skip,
        int take,
        DateTime nowUtc,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the total count of education accounts that match all active rules.
    /// </summary>
    Task<int> CountMatchingAccountsAsync(
        IReadOnlyList<CampaignRuleProjection> rules,
        DateTime nowUtc,
        CancellationToken cancellationToken = default);
}
