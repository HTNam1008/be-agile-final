using Moe.Modules.EducationAccountTopUp.Application.TopUps.GetCampaigns;
using Moe.Modules.EducationAccountTopUp.Application.TopUps.PreviewCampaign;

namespace Moe.Modules.EducationAccountTopUp.IGateway.TopUps;

public interface ITopUpCampaignReader
{
    Task<IReadOnlyList<CampaignListItem>> GetCampaignsAsync(
        IReadOnlyCollection<long>? accessibleOrgIds,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CampaignRuleProjection>> GetRulesAsync(long campaignId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ActiveRecipientProjection>> GetActiveRecipientsAsync(long campaignId, CancellationToken cancellationToken = default);

    /// <summary>Returns active recipients for the campaign with their amounts, for preview. Paged.</summary>
    Task<(int TotalCount, IReadOnlyList<PreviewFixedRecipient> Items)> GetFixedRecipientsForPreviewAsync(
        long campaignId,
        int skip,
        int take,
        CancellationToken cancellationToken = default);

    /// <summary>Returns the campaign summary needed for preview access/routing. Null if not found.</summary>
    Task<CampaignPreviewSummary?> GetPreviewSummaryAsync(long campaignId, CancellationToken cancellationToken = default);
}
