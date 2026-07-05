using Microsoft.EntityFrameworkCore;
using Moe.Modules.EducationAccountTopUp.Application.TopUps.GetCampaigns;
using Moe.Modules.EducationAccountTopUp.Application.TopUps.PreviewCampaign;
using Moe.Modules.EducationAccountTopUp.Domain.EducationAccounts;
using Moe.Modules.EducationAccountTopUp.Domain.TopUps;
using Moe.Modules.EducationAccountTopUp.IGateway.TopUps;
using Moe.StudentFinance.Persistence;

namespace Moe.Modules.EducationAccountTopUp.Infrastructure.TopUps;

internal sealed class TopUpCampaignReader(MoeDbContext dbContext) : ITopUpCampaignReader
{
    public async Task<CampaignListItem?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        return await dbContext.Set<TopUpCampaign>().AsNoTracking()
            .Where(c => c.Id == id)
            .Select(c => new CampaignListItem(
                c.Id,
                c.OrganizationId,
                c.CampaignCode,
                c.CampaignName,
                c.Description,
                c.RecipientModeCode,
                c.DefaultTopUpAmount,
                c.Reason,
                c.ScheduleTypeCode,
                c.StartDate,
                c.EndDate,
                c.FrequencyCode,
                c.FrequencyInterval,
                c.WeeklyDayOfWeek,
                c.MonthlyDay,
                c.NextRunAtUtc,
                c.CampaignStatusCode,
                c.CampaignVersion,
                c.DeliveryTypeCode,
                c.MaxTotalAmount,
                c.CreatedByLoginAccountId,
                c.UpdatedByLoginAccountId,
                c.CreatedAtUtc,
                c.UpdatedAtUtc,
                dbContext.Set<TopUpRun>()
                    .Where(r => r.TopUpCampaignId == c.Id)
                    .SelectMany(r => dbContext.Set<TopUpTransaction>().Where(t => t.TopUpRunId == r.Id && t.TransactionStatusCode == TopUpTransactionStatusCodes.Completed))
                    .Select(t => t.EducationAccountId)
                    .Distinct()
                    .Count()))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<CampaignListResult> GetCampaignsAsync(
        IReadOnlyCollection<long>? accessibleOrgIds,
        int pageNumber = 1,
        int pageSize = 50,
        string? search = null,
        string? status = null,
        DateOnly? dateFrom = null,
        DateOnly? dateTo = null,
        string? sortBy = null,
        string? sortDirection = null,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.Set<TopUpCampaign>().AsNoTracking();

        if (accessibleOrgIds != null && accessibleOrgIds.Count > 0)
            query = query.Where(c => accessibleOrgIds.Contains(c.OrganizationId));

        if (!string.IsNullOrWhiteSpace(search))
        {
            var lowerSearch = search.ToLower();
            query = query.Where(c => c.CampaignCode.ToLower().Contains(lowerSearch)
                                  || c.CampaignName.ToLower().Contains(lowerSearch));
        }

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(c => c.CampaignStatusCode == status);

        if (dateFrom.HasValue)
            query = query.Where(c => c.StartDate >= dateFrom.Value);

        if (dateTo.HasValue)
            query = query.Where(c => c.StartDate <= dateTo.Value);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await ApplyCampaignSort(query, sortBy, sortDirection)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new CampaignListItem(
                c.Id,
                c.OrganizationId,
                c.CampaignCode,
                c.CampaignName,
                c.Description,
                c.RecipientModeCode,
                c.DefaultTopUpAmount,
                c.Reason,
                c.ScheduleTypeCode,
                c.StartDate,
                c.EndDate,
                c.FrequencyCode,
                c.FrequencyInterval,
                c.WeeklyDayOfWeek,
                c.MonthlyDay,
                c.NextRunAtUtc,
                c.CampaignStatusCode,
                c.CampaignVersion,
                c.DeliveryTypeCode,
                c.MaxTotalAmount,
                c.CreatedByLoginAccountId,
                c.UpdatedByLoginAccountId,
                c.CreatedAtUtc,
                c.UpdatedAtUtc,
                dbContext.Set<TopUpRun>()
                    .Where(r => r.TopUpCampaignId == c.Id)
                    .SelectMany(r => dbContext.Set<TopUpTransaction>().Where(t => t.TopUpRunId == r.Id && t.TransactionStatusCode == TopUpTransactionStatusCodes.Completed))
                    .Select(t => t.EducationAccountId)
                    .Distinct()
                    .Count()))
            .ToListAsync(cancellationToken);

        return new CampaignListResult(items, totalCount, pageNumber, pageSize);
    }

    private static IOrderedQueryable<TopUpCampaign> ApplyCampaignSort(
        IQueryable<TopUpCampaign> query,
        string? sortBy,
        string? sortDirection)
    {
        bool descending = string.Equals(sortDirection, "desc", StringComparison.OrdinalIgnoreCase);
        string key = sortBy?.Trim().ToLowerInvariant() ?? string.Empty;

        return key switch
        {
            "campaign" or "campaigncode" => descending
                ? query.OrderByDescending(c => c.CampaignCode).ThenByDescending(c => c.Id)
                : query.OrderBy(c => c.CampaignCode).ThenByDescending(c => c.Id),
            "campaignname" or "name" => descending
                ? query.OrderByDescending(c => c.CampaignName).ThenByDescending(c => c.Id)
                : query.OrderBy(c => c.CampaignName).ThenByDescending(c => c.Id),
            "type" or "recipientmode" => descending
                ? query.OrderByDescending(c => c.RecipientModeCode).ThenByDescending(c => c.Id)
                : query.OrderBy(c => c.RecipientModeCode).ThenByDescending(c => c.Id),
            "schedule" => descending
                ? query.OrderByDescending(c => c.ScheduleTypeCode).ThenByDescending(c => c.Id)
                : query.OrderBy(c => c.ScheduleTypeCode).ThenByDescending(c => c.Id),
            "start" or "startdate" => descending
                ? query.OrderByDescending(c => c.StartDate).ThenByDescending(c => c.Id)
                : query.OrderBy(c => c.StartDate).ThenByDescending(c => c.Id),
            "end" or "enddate" => descending
                ? query.OrderByDescending(c => c.EndDate).ThenByDescending(c => c.Id)
                : query.OrderBy(c => c.EndDate).ThenByDescending(c => c.Id),
            "amount" => descending
                ? query.OrderByDescending(c => c.MaxTotalAmount > 0 ? c.MaxTotalAmount : c.DefaultTopUpAmount).ThenByDescending(c => c.Id)
                : query.OrderBy(c => c.MaxTotalAmount > 0 ? c.MaxTotalAmount : c.DefaultTopUpAmount).ThenByDescending(c => c.Id),
            "status" => descending
                ? query.OrderByDescending(c => c.CampaignStatusCode).ThenByDescending(c => c.Id)
                : query.OrderBy(c => c.CampaignStatusCode).ThenByDescending(c => c.Id),
            "created" or "createdat" => descending
                ? query.OrderByDescending(c => c.CreatedAtUtc).ThenByDescending(c => c.Id)
                : query.OrderBy(c => c.CreatedAtUtc).ThenByDescending(c => c.Id),
            _ => query.OrderByDescending(c => c.Id)
        };
    }

    public async Task<IReadOnlyList<CampaignRuleGroupProjection>> GetRulesAsync(long campaignId, CancellationToken cancellationToken = default)
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

    public async Task<IReadOnlyList<ActiveRecipientProjection>> GetActiveRecipientsAsync(long campaignId, CancellationToken cancellationToken = default)
    {
        return await dbContext.Set<TopUpCampaignRecipient>()
            .AsNoTracking()
            .Where(x => x.TopUpCampaignId == campaignId && x.IsActive)
            .Select(x => new ActiveRecipientProjection(
                x.EducationAccountId,
                x.AmountOverride))
            .ToListAsync(cancellationToken);
    }

    public async Task<(int TotalCount, IReadOnlyList<PreviewFixedRecipient> Items)> GetFixedRecipientsForPreviewAsync(
        long campaignId,
        int skip,
        int take,
        CancellationToken cancellationToken = default)
    {
        // Join recipients to active accounts in a single query
        var baseQuery =
            from recipient in dbContext.Set<TopUpCampaignRecipient>().AsNoTracking()
            join campaign in dbContext.Set<TopUpCampaign>().AsNoTracking()
                on recipient.TopUpCampaignId equals campaign.Id
            join account in dbContext.Set<EducationAccount>().AsNoTracking()
                on recipient.EducationAccountId equals account.Id
            where recipient.TopUpCampaignId == campaignId
                  && recipient.IsActive
                  && account.StatusCode == AccountStatuses.Active
            select new
            {
                recipient.EducationAccountId,
                Amount = recipient.AmountOverride ?? campaign.DefaultTopUpAmount
            };

        int totalCount = await baseQuery.CountAsync(cancellationToken);

        List<PreviewFixedRecipient> items = await baseQuery
            .OrderBy(x => x.EducationAccountId)
            .Skip(skip)
            .Take(take)
            .Select(x => new PreviewFixedRecipient(x.EducationAccountId, x.Amount))
            .ToListAsync(cancellationToken);

        return (totalCount, items);
    }

    public async Task<CampaignPreviewSummary?> GetPreviewSummaryAsync(long campaignId, CancellationToken cancellationToken = default)
    {
        return await dbContext.Set<TopUpCampaign>()
            .AsNoTracking()
            .Where(c => c.Id == campaignId)
            .Select(c => new CampaignPreviewSummary(
                c.Id,
                c.OrganizationId,
                c.RecipientModeCode,
                c.DefaultTopUpAmount))
            .SingleOrDefaultAsync(cancellationToken);
    }
}
