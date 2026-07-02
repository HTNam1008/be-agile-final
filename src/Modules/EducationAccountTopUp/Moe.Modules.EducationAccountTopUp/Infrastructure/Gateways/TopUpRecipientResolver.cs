using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moe.Application.Abstractions.Clock;
using Moe.Modules.EducationAccountTopUp.Application.RunExecution;
using Moe.Modules.EducationAccountTopUp.Contracts.TopUps.Enums;
using Moe.Modules.EducationAccountTopUp.Domain.EducationAccounts;
using Moe.Modules.EducationAccountTopUp.Domain.TopUps;
using Moe.Modules.EducationAccountTopUp.IGateway;
using Moe.Modules.EducationAccountTopUp.IGateway.TopUps;
using Moe.StudentFinance.Persistence;

namespace Moe.Modules.EducationAccountTopUp.Infrastructure.Gateways;

internal sealed class TopUpRecipientResolver(
    MoeDbContext dbContext,
    IDynamicRuleFilter dynamicRuleFilter,
    IClock clock,
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

        IReadOnlyList<CampaignRuleGroupProjection> groups = await GetActiveRuleGroupsAsync(campaign.Id, cancellationToken);
        if (groups.Count == 0)
            return 0;

        return await dynamicRuleFilter.CountMatchingAccountsAsync(groups, clock.UtcNow.UtcDateTime, cancellationToken);
    }

    public async Task<decimal> GetTotalResolvedAmountAsync(
        long campaignId,
        long runId,
        CancellationToken cancellationToken = default)
    {
        TopUpCampaign? campaign = await GetCampaignAsync(campaignId, cancellationToken);
        if (campaign is null)
        {
            return 0m;
        }

        if (IsFixedSelection(campaign))
        {
            return await GetFixedRecipientAmountSumQuery(campaign.Id, campaign.DefaultTopUpAmount)
                .SumAsync(cancellationToken);
        }

        int count = await GetTotalRecipientCountAsync(campaignId, runId, cancellationToken);
        return count * campaign.DefaultTopUpAmount;
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

    private IQueryable<decimal> GetFixedRecipientAmountSumQuery(
        long campaignId,
        decimal defaultTopUpAmount)
    {
        return from recipient in dbContext.Set<TopUpCampaignRecipient>().AsNoTracking()
               join account in dbContext.Set<EducationAccount>().AsNoTracking()
                   on recipient.EducationAccountId equals account.Id
               where recipient.TopUpCampaignId == campaignId
                   && recipient.IsActive
                   && account.StatusCode == AccountStatuses.Active
               select (recipient.AmountOverride ?? defaultTopUpAmount);
    }

    private async Task<IReadOnlyList<RecipientInfo>> GetDynamicRecipientsAsync(
        TopUpCampaign campaign,
        int chunkSize,
        int offset,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<CampaignRuleGroupProjection> groups = await GetActiveRuleGroupsAsync(campaign.Id, cancellationToken);
        if (groups.Count == 0)
            return [];

        IReadOnlyList<long> accountIds = await dynamicRuleFilter.FilterAccountIdsAsync(
            groups, offset, chunkSize, clock.UtcNow.UtcDateTime, cancellationToken);

        return accountIds
            .Select(id => new RecipientInfo
            {
                EducationAccountId = id,
                Amount = campaign.DefaultTopUpAmount,
                OrganizationUnitId = campaign.OrganizationId,
                CampaignReason = campaign.Reason
            })
            .ToList();
    }

    private async Task<IReadOnlyList<CampaignRuleGroupProjection>> GetActiveRuleGroupsAsync(
        long campaignId,
        CancellationToken cancellationToken)
    {
        var rows = await (
            from ruleGroup in dbContext.Set<TopUpRuleGroup>().AsNoTracking()
            join rule in dbContext.Set<TopUpCampaignRule>().AsNoTracking()
                on ruleGroup.Id equals rule.TopUpRuleGroupId
            where ruleGroup.TopUpCampaignId == campaignId
            orderby ruleGroup.DisplayOrder, rule.DisplayOrder, rule.Id
            select new
            {
                GroupId = ruleGroup.Id,
                GroupDisplayOrder = ruleGroup.DisplayOrder,
                Rule = new CampaignRuleProjection(
                    rule.Id,
                    ruleGroup.Id,
                    rule.DisplayOrder,
                    rule.CriterionCode,
                    rule.OperatorCode,
                    rule.NumericValueFrom,
                    rule.NumericValueTo,
                    rule.TextValue)
            })
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(x => new { x.GroupId, x.GroupDisplayOrder })
            .OrderBy(x => x.Key.GroupDisplayOrder)
            .Select(x => new CampaignRuleGroupProjection(
                x.Key.GroupId,
                x.Key.GroupDisplayOrder,
                x.Select(row => row.Rule).OrderBy(rule => rule.DisplayOrder).ToList()))
            .ToList();
    }

    private static bool IsFixedSelection(TopUpCampaign campaign)
    {
        return string.Equals(
            campaign.RecipientModeCode,
            RecipientModeCode.FixedSelection.ToString(),
            StringComparison.OrdinalIgnoreCase);
    }
}
