using Microsoft.EntityFrameworkCore;
using Moe.Modules.EducationAccountTopUp.Application.TopUps.GetCampaigns;
using Moe.Modules.EducationAccountTopUp.Contracts.TopUps.Enums;
using Moe.Modules.EducationAccountTopUp.Domain.TopUps;
using Moe.Modules.EducationAccountTopUp.IGateway.Repositories;
using Moe.StudentFinance.Persistence;

namespace Moe.Modules.EducationAccountTopUp.Infrastructure.Repositories;

internal sealed class TopUpCampaignRepository(MoeDbContext dbContext) : ITopUpCampaignRepository, ITopUpCampaignRuleGroupRepository, ITopUpCampaignDeletionRepository
{
    public Task<TopUpCampaign?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        return dbContext.Set<TopUpCampaign>().SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<TopUpCampaign>> GetByIdsAsync(
        IReadOnlyList<long> ids,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.Set<TopUpCampaign>()
            .AsNoTracking()
            .Where(x => ids.Contains(x.Id))
            .ToListAsync(cancellationToken);
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
            .Where(c => c.CampaignStatusCode == TopUpCampaignStatusCodes.Active
                && c.RecipientModeCode != RecipientModeCode.DynamicRules.ToString()
                && c.NextRunAtUtc != null
                && c.NextRunAtUtc <= utcNow)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<TopUpCampaign>> GetDueForAssessmentAsync(DateOnly today, CancellationToken cancellationToken = default)
    {
        return await dbContext.Set<TopUpCampaign>()
            .Where(c => c.CampaignStatusCode == TopUpCampaignStatusCodes.Active
                && c.RecipientModeCode == RecipientModeCode.DynamicRules.ToString()
                && c.DeliveryTypeCode != DeliveryType.Instant
                && c.StartDate <= today
                && (c.EndDate == null || c.EndDate >= today))
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(TopUpCampaign campaign, CancellationToken cancellationToken = default)
    {
        await dbContext.Set<TopUpCampaign>().AddAsync(campaign, cancellationToken);
    }

    public async Task DeleteDraftAsync(TopUpCampaign campaign, CancellationToken cancellationToken = default)
    {
        if (dbContext.Database.ProviderName?.Contains("InMemory", StringComparison.OrdinalIgnoreCase) != true)
        {
            await dbContext.Set<TopUpCampaignRecipient>()
                .IgnoreQueryFilters()
                .Where(x => x.TopUpCampaignId == campaign.Id)
                .ExecuteDeleteAsync(cancellationToken);

            await DeleteRuleGroupsByCampaignIdAsync(campaign.Id, cancellationToken);
            dbContext.Set<TopUpCampaign>().Remove(campaign);
            return;
        }

        var recipients = await dbContext.Set<TopUpCampaignRecipient>()
            .IgnoreQueryFilters()
            .Where(x => x.TopUpCampaignId == campaign.Id)
            .ToListAsync(cancellationToken);

        dbContext.Set<TopUpCampaignRecipient>().RemoveRange(recipients);
        await DeleteRuleGroupsByCampaignIdAsync(campaign.Id, cancellationToken);
        dbContext.Set<TopUpCampaign>().Remove(campaign);
    }

    public Task<int> CountActiveRulesAsync(long campaignId, CancellationToken cancellationToken = default)
    {
        return dbContext.Set<TopUpCampaignRule>()
            .CountAsync(x => x.TopUpCampaignId == campaignId, cancellationToken);
    }

    public Task<int> CountActiveRecipientsAsync(long campaignId, CancellationToken cancellationToken = default)
    {
        return dbContext.Set<TopUpCampaignRecipient>()
            .CountAsync(x => x.TopUpCampaignId == campaignId && x.IsActive, cancellationToken);
    }

    public async Task DeleteRuleGroupsByCampaignIdAsync(long campaignId, CancellationToken cancellationToken = default)
    {
        if (dbContext.Database.ProviderName?.Contains("InMemory", StringComparison.OrdinalIgnoreCase) != true)
        {
            await dbContext.Set<TopUpCampaignRule>()
                .Where(x => x.TopUpCampaignId == campaignId)
                .ExecuteDeleteAsync(cancellationToken);

            await dbContext.Set<TopUpRuleGroup>()
                .Where(x => x.TopUpCampaignId == campaignId)
                .ExecuteDeleteAsync(cancellationToken);
            return;
        }

        var rules = await dbContext.Set<TopUpCampaignRule>()
            .Where(x => x.TopUpCampaignId == campaignId)
            .ToListAsync(cancellationToken);
        var groups = await dbContext.Set<TopUpRuleGroup>()
            .Where(x => x.TopUpCampaignId == campaignId)
            .ToListAsync(cancellationToken);

        dbContext.Set<TopUpCampaignRule>().RemoveRange(rules);
        dbContext.Set<TopUpRuleGroup>().RemoveRange(groups);
    }

    public async Task AddRuleGroupAsync(TopUpRuleGroup group, CancellationToken cancellationToken = default)
    {
        await dbContext.Set<TopUpRuleGroup>().AddAsync(group, cancellationToken);
    }

    public async Task<Dictionary<long, decimal>> GetAmountOverridesByCampaignAsync(long campaignId, CancellationToken cancellationToken = default)
    {
        return await dbContext.Set<TopUpCampaignRecipient>()
            .IgnoreQueryFilters()
            .Where(x => x.TopUpCampaignId == campaignId && x.AmountOverride != null && x.DeletedAtUtc == null)
            .ToDictionaryAsync(x => x.EducationAccountId, x => x.AmountOverride!.Value, cancellationToken);
    }

    public async Task<IReadOnlyList<TopUpCampaignRecipient>> GetRecipientsAsync(long campaignId, CancellationToken cancellationToken = default)
    {
        return await dbContext.Set<TopUpCampaignRecipient>()
            .IgnoreQueryFilters()
            .Where(x => x.TopUpCampaignId == campaignId)
            .ToListAsync(cancellationToken);
    }

    public Task RemoveRecipientsAsync(IEnumerable<TopUpCampaignRecipient> recipients, long userId, DateTime nowUtc, CancellationToken cancellationToken = default)
    {
        foreach (var recipient in recipients)
        {
            recipient.SoftDelete(userId, nowUtc);
        }
        return Task.CompletedTask;
    }

    public async Task AddRecipientAsync(TopUpCampaignRecipient recipient, CancellationToken cancellationToken = default)
    {
        await dbContext.Set<TopUpCampaignRecipient>().AddAsync(recipient, cancellationToken);
    }
}
