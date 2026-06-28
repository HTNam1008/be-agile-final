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
                c.NextRunAtUtc,
                c.CampaignStatusCode,
                c.CampaignVersion,
                c.DeliveryTypeCode,
                c.MaxTotalAmount,
                c.CreatedAtUtc,
                c.UpdatedAtUtc))
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

        var items = await query
            .OrderByDescending(c => c.Id)
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
                c.NextRunAtUtc,
                c.CampaignStatusCode,
                c.CampaignVersion,
                c.DeliveryTypeCode,
                c.MaxTotalAmount,
                c.CreatedAtUtc,
                c.UpdatedAtUtc))
            .ToListAsync(cancellationToken);

        return new CampaignListResult(items, totalCount, pageNumber, pageSize);
    }

    public async Task<IReadOnlyList<CampaignRuleProjection>> GetRulesAsync(long campaignId, CancellationToken cancellationToken = default)
    {
        return await dbContext.Set<TopUpCampaignRule>()
            .AsNoTracking()
            .Where(x => x.TopUpCampaignId == campaignId && x.IsActive)
            .Select(x => new CampaignRuleProjection(
                x.Id,
                x.CriterionCode,
                x.OperatorCode,
                x.NumericValueFrom,
                x.NumericValueTo,
                x.TextValue))
            .ToListAsync(cancellationToken);
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
