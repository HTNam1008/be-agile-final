using Moe.Modules.EducationAccountTopUp.Application.TopUps.GetCampaigns;
using Moe.Modules.EducationAccountTopUp.Domain.TopUps;

namespace Moe.Modules.EducationAccountTopUp.IGateway.Repositories;

public interface ITopUpCampaignRepository
{
    Task<TopUpCampaign?> GetByIdAsync(long id, CancellationToken cancellationToken = default);

    Task<bool> CampaignCodeExistsAsync(
        long organizationId,
        string campaignCode,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CampaignListItem>> ListAsync(
        IReadOnlyCollection<long>? accessibleOrgIds,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TopUpCampaignRule>> GetRulesAsync(long campaignId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TopUpCampaignRecipient>> GetActiveRecipientsAsync(long campaignId, CancellationToken cancellationToken = default);

    Task<int> CountActiveRulesAsync(long campaignId, CancellationToken cancellationToken = default);

    Task<int> CountActiveRecipientsAsync(long campaignId, CancellationToken cancellationToken = default);

    Task AddAsync(TopUpCampaign campaign, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
