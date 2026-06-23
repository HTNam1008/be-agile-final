using Microsoft.EntityFrameworkCore;
using Moe.Modules.EducationAccountTopUp.Application.TopUps.GetCampaigns;
using Moe.Modules.EducationAccountTopUp.Domain.TopUps;
using Moe.Modules.EducationAccountTopUp.IGateway.Repositories;
using Moe.StudentFinance.Persistence;

namespace Moe.Modules.EducationAccountTopUp.Infrastructure.Repositories;

internal sealed class TopUpCampaignRepository(MoeDbContext dbContext) : ITopUpCampaignRepository
{
    public Task<TopUpCampaign?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        return dbContext.Set<TopUpCampaign>().SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public Task<bool> CampaignCodeExistsAsync(
        long organizationId,
        string campaignCode,
        CancellationToken cancellationToken = default)
    {
        return dbContext.Set<TopUpCampaign>()
            .AnyAsync(c => c.OrganizationId == organizationId
                && c.CampaignCode == campaignCode,
                cancellationToken);
    }

    public async Task<IReadOnlyList<TopUpCampaign>> GetDueCampaignsAsync(DateTime utcNow, CancellationToken cancellationToken = default)
    {
        return await dbContext.Set<TopUpCampaign>()
            .Where(c => c.CampaignStatusCode == TopUpCampaignStatusCodes.Active && c.NextRunAtUtc != null && c.NextRunAtUtc <= utcNow)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(TopUpCampaign campaign, CancellationToken cancellationToken = default)
    {
        await dbContext.Set<TopUpCampaign>().AddAsync(campaign, cancellationToken);
    }

    public Task<int> CountActiveRulesAsync(long campaignId, CancellationToken cancellationToken = default)
    {
        return dbContext.Set<TopUpCampaignRule>()
            .CountAsync(x => x.TopUpCampaignId == campaignId && x.IsActive, cancellationToken);
    }

    public Task<int> CountActiveRecipientsAsync(long campaignId, CancellationToken cancellationToken = default)
    {
        return dbContext.Set<TopUpCampaignRecipient>()
            .CountAsync(x => x.TopUpCampaignId == campaignId && x.IsActive, cancellationToken);
    }

    public async Task<IReadOnlyList<TopUpCampaignRule>> GetRulesAsync(long campaignId, CancellationToken cancellationToken = default)
    {
        return await dbContext.Set<TopUpCampaignRule>()
            .Where(x => x.TopUpCampaignId == campaignId)
            .ToListAsync(cancellationToken);
    }

    public Task RemoveRulesAsync(IEnumerable<TopUpCampaignRule> rules, CancellationToken cancellationToken = default)
    {
        dbContext.Set<TopUpCampaignRule>().RemoveRange(rules);
        return Task.CompletedTask;
    }

    public async Task AddRuleAsync(TopUpCampaignRule rule, CancellationToken cancellationToken = default)
    {
        await dbContext.Set<TopUpCampaignRule>().AddAsync(rule, cancellationToken);
    }

    public async Task<IReadOnlyList<TopUpCampaignRecipient>> GetRecipientsAsync(long campaignId, CancellationToken cancellationToken = default)
    {
        return await dbContext.Set<TopUpCampaignRecipient>()
            .Where(x => x.TopUpCampaignId == campaignId)
            .ToListAsync(cancellationToken);
    }

    public Task RemoveRecipientsAsync(IEnumerable<TopUpCampaignRecipient> recipients, CancellationToken cancellationToken = default)
    {
        dbContext.Set<TopUpCampaignRecipient>().RemoveRange(recipients);
        return Task.CompletedTask;
    }

    public async Task AddRecipientAsync(TopUpCampaignRecipient recipient, CancellationToken cancellationToken = default)
    {
        await dbContext.Set<TopUpCampaignRecipient>().AddAsync(recipient, cancellationToken);
    }
}
