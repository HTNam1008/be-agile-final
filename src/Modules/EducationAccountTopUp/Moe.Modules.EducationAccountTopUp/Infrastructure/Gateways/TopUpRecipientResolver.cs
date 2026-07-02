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

        List<TopUpCampaignRule> rules = await GetActiveRulesAsync(campaign.Id, cancellationToken);
        if (rules.Count == 0)
            return 0;

        var projections = rules
            .Select(r => new CampaignRuleProjection(r.Id, r.CriterionCode, r.OperatorCode, r.NumericValueFrom, r.NumericValueTo, r.TextValue))
            .ToList();

        return await dynamicRuleFilter.CountMatchingAccountsAsync(projections, clock.UtcNow.UtcDateTime, cancellationToken);
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
        List<TopUpCampaignRule> rules = await GetActiveRulesAsync(campaign.Id, cancellationToken);
        if (rules.Count == 0)
            return [];

        // Map domain rules to CampaignRuleProjection for IDynamicRuleFilter
        var projections = rules
            .Select(r => new CampaignRuleProjection(r.Id, r.CriterionCode, r.OperatorCode, r.NumericValueFrom, r.NumericValueTo, r.TextValue))
            .ToList();

        IReadOnlyList<long> accountIds = await dynamicRuleFilter.FilterAccountIdsAsync(
            projections, offset, chunkSize, clock.UtcNow.UtcDateTime, cancellationToken);

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

    private Task<List<TopUpCampaignRule>> GetActiveRulesAsync(
        long campaignId,
        CancellationToken cancellationToken)
    {
        return dbContext.Set<TopUpCampaignRule>()
            .AsNoTracking()
            .Where(x => x.TopUpCampaignId == campaignId && x.IsActive)
            .ToListAsync(cancellationToken);
    }

    private static bool IsFixedSelection(TopUpCampaign campaign)
    {
        return string.Equals(
            campaign.RecipientModeCode,
            RecipientModeCode.FixedSelection.ToString(),
            StringComparison.OrdinalIgnoreCase);
    }
}
