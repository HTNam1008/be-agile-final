using Moe.Modules.EducationAccountTopUp.Application.TopUps.GetCampaigns;
using Moe.Modules.EducationAccountTopUp.Application.TopUps.PreviewCampaign;

namespace Moe.Modules.EducationAccountTopUp.IGateway.TopUps;

public interface ITopUpCampaignReader
{
    Task<CampaignListItem?> GetByIdAsync(long id, CancellationToken cancellationToken = default);
    Task<CampaignListResult> GetCampaignsAsync(
        IReadOnlyCollection<long>? accessibleOrgIds,
        int pageNumber = 1,
        int pageSize = 50,
        string? search = null,
        string? status = null,
        DateOnly? dateFrom = null,
        DateOnly? dateTo = null,
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
