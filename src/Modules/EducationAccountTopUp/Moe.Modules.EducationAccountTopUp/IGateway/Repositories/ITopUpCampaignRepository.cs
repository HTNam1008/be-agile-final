using Moe.Modules.EducationAccountTopUp.Application.TopUps.GetCampaigns;
using Moe.Modules.EducationAccountTopUp.Domain.TopUps;

namespace Moe.Modules.EducationAccountTopUp.IGateway.Repositories;

public interface ITopUpCampaignRepository
{
    Task<TopUpCampaign?> GetByIdAsync(long id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TopUpCampaign>> GetByIdsAsync(
        IReadOnlyList<long> ids,
        CancellationToken cancellationToken = default);

    Task<bool> CampaignCodeExistsAsync(
        long organizationId,
        string campaignCode,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TopUpCampaign>> GetDueCampaignsAsync(DateTime utcNow, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TopUpCampaign>> GetDueForAssessmentAsync(DateOnly today, CancellationToken cancellationToken = default);

    Task<int> CountActiveRulesAsync(long campaignId, CancellationToken cancellationToken = default);

    Task<int> CountActiveRecipientsAsync(long campaignId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TopUpCampaignRule>> GetRulesAsync(long campaignId, CancellationToken cancellationToken = default);
    Task RemoveRulesAsync(IEnumerable<TopUpCampaignRule> rules, CancellationToken cancellationToken = default);
    Task AddRuleAsync(TopUpCampaignRule rule, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TopUpCampaignRecipient>> GetRecipientsAsync(long campaignId, CancellationToken cancellationToken = default);
    Task<Dictionary<long, decimal>> GetAmountOverridesByCampaignAsync(long campaignId, CancellationToken cancellationToken = default);
    Task RemoveRecipientsAsync(IEnumerable<TopUpCampaignRecipient> recipients, long userId, DateTime nowUtc, CancellationToken cancellationToken = default);
    Task AddRecipientAsync(TopUpCampaignRecipient recipient, CancellationToken cancellationToken = default);

    Task AddAsync(TopUpCampaign campaign, CancellationToken cancellationToken = default);
}
