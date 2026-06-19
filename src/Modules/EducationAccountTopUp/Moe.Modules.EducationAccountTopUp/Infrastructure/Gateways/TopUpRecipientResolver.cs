using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moe.Modules.EducationAccountTopUp.Application.RunExecution;
using Moe.Modules.EducationAccountTopUp.Application.TopUps;
using Moe.Modules.EducationAccountTopUp.Contracts.TopUps.Enums;
using Moe.Modules.EducationAccountTopUp.Domain.EducationAccounts;
using Moe.Modules.EducationAccountTopUp.Domain.TopUps;
using Moe.Modules.EducationAccountTopUp.IGateway;
using Moe.StudentFinance.Persistence;

namespace Moe.Modules.EducationAccountTopUp.Infrastructure.Gateways;

internal sealed class TopUpRecipientResolver(
    MoeDbContext dbContext,
    ILogger<TopUpRecipientResolver> logger) : IRecipientResolver
{
    public async Task<IReadOnlyList<RecipientInfo>> GetRecipientsChunkAsync(
        long campaignId,
        long runId,
        int chunkSize,
        int offset,
        CancellationToken cancellationToken = default)
    {
        TopUpCampaign? campaign = await GetCampaignAsync(campaignId, cancellationToken);
        if (campaign is null)
        {
            logger.LogWarning(
                "Cannot resolve recipients for run {TopUpRunId}; campaign {CampaignId} was not found",
                runId,
                campaignId);
            return [];
        }

        return IsFixedSelection(campaign)
            ? await GetFixedRecipientsAsync(campaign, chunkSize, offset, cancellationToken)
            : await GetDynamicRecipientsAsync(campaign, chunkSize, offset, cancellationToken);
    }

    public async Task<int> GetTotalRecipientCountAsync(
        long campaignId,
        long runId,
        CancellationToken cancellationToken = default)
    {
        TopUpCampaign? campaign = await GetCampaignAsync(campaignId, cancellationToken);
        if (campaign is null)
        {
            logger.LogWarning(
                "Cannot count recipients for run {TopUpRunId}; campaign {CampaignId} was not found",
                runId,
                campaignId);
            return 0;
        }

        if (IsFixedSelection(campaign))
        {
            return await GetFixedRecipientQuery(campaign.Id, campaign.DefaultTopUpAmount, campaign.OrganizationId, campaign.Reason)
                .CountAsync(cancellationToken);
        }

        List<TopUpCampaignRule> rules = await GetActiveRulesAsync(campaign.Id, cancellationToken);
        if (rules.Count == 0)
        {
            return 0;
        }

        IQueryable<EducationAccount> dynamicQuery = BuildDynamicAccountQuery(rules);
        return await dynamicQuery.CountAsync(cancellationToken);
    }

    private Task<TopUpCampaign?> GetCampaignAsync(long campaignId, CancellationToken cancellationToken)
    {
        return dbContext.Set<TopUpCampaign>()
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == campaignId, cancellationToken);
    }

    private async Task<IReadOnlyList<RecipientInfo>> GetFixedRecipientsAsync(
        TopUpCampaign campaign,
        int chunkSize,
        int offset,
        CancellationToken cancellationToken)
    {
        return await GetFixedRecipientQuery(
                campaign.Id,
                campaign.DefaultTopUpAmount,
                campaign.OrganizationId,
                campaign.Reason)
            .Skip(offset)
            .Take(chunkSize)
            .ToListAsync(cancellationToken);
    }

    private IQueryable<RecipientInfo> GetFixedRecipientQuery(
        long campaignId,
        decimal defaultTopUpAmount,
        long organizationId,
        string campaignReason)
    {
        return from recipient in dbContext.Set<TopUpCampaignRecipient>().AsNoTracking()
               join account in dbContext.Set<EducationAccount>().AsNoTracking()
                   on recipient.EducationAccountId equals account.Id
               where recipient.TopUpCampaignId == campaignId
                   && recipient.IsActive
                   && account.StatusCode == AccountStatuses.Active
               orderby recipient.EducationAccountId
               select new RecipientInfo
               {
                   EducationAccountId = account.Id,
                   Amount = recipient.AmountOverride ?? defaultTopUpAmount,
                   OrganizationUnitId = organizationId,
                   CampaignReason = campaignReason
               };
    }

    private async Task<IReadOnlyList<RecipientInfo>> GetDynamicRecipientsAsync(
        TopUpCampaign campaign,
        int chunkSize,
        int offset,
        CancellationToken cancellationToken)
    {
        List<TopUpCampaignRule> rules = await GetActiveRulesAsync(campaign.Id, cancellationToken);
        if (rules.Count == 0)
        {
            return [];
        }

        IQueryable<EducationAccount> dynamicQuery = BuildDynamicAccountQuery(rules);
        return await dynamicQuery
            .OrderBy(x => x.Id)
            .Skip(offset)
            .Take(chunkSize)
            .Select(account => new RecipientInfo
            {
                EducationAccountId = account.Id,
                Amount = campaign.DefaultTopUpAmount,
                OrganizationUnitId = campaign.OrganizationId,
                CampaignReason = campaign.Reason
            })
            .ToListAsync(cancellationToken);
    }

    private Task<List<TopUpCampaignRule>> GetActiveRulesAsync(
        long campaignId,
        CancellationToken cancellationToken)
    {
        return dbContext.Set<TopUpCampaignRule>()
            .AsNoTracking()
            .Where(x => x.TopUpCampaignId == campaignId && x.IsActive)
            .ToListAsync(cancellationToken);
    }

    private IQueryable<EducationAccount> BuildDynamicAccountQuery(List<TopUpCampaignRule> rules)
    {
        IQueryable<EducationAccount> query = dbContext.Set<EducationAccount>()
            .AsNoTracking()
            .Where(x => x.StatusCode == AccountStatuses.Active);

        return DynamicRuleEvaluator.ApplyRules(dbContext, query, rules, DateTime.UtcNow);
    }

    private static bool IsFixedSelection(TopUpCampaign campaign)
    {
        return string.Equals(
            campaign.RecipientModeCode,
            RecipientModeCode.FixedSelection.ToString(),
            StringComparison.OrdinalIgnoreCase);
    }
}
