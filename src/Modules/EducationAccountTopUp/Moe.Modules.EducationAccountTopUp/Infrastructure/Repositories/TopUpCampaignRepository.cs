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

    public async Task<IReadOnlyList<CampaignListItem>> ListAsync(
        IReadOnlyCollection<long>? accessibleOrgIds,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.Set<TopUpCampaign>().AsNoTracking();

        if (accessibleOrgIds is { Count: > 0 })
            query = query.Where(c => accessibleOrgIds.Contains(c.OrganizationId));

        return await query
            .OrderByDescending(c => c.Id)
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
                c.CreatedAtUtc,
                c.UpdatedAtUtc))
            .ToListAsync(cancellationToken);
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

    public async Task AddAsync(TopUpCampaign campaign, CancellationToken cancellationToken = default)
    {
        await dbContext.Set<TopUpCampaign>().AddAsync(campaign, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<TopUpCampaignRule>> GetRulesAsync(long campaignId, CancellationToken cancellationToken = default)
    {
        return await dbContext.Set<TopUpCampaignRule>()
            .AsNoTracking()
            .Where(x => x.TopUpCampaignId == campaignId && x.IsActive)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<TopUpCampaignRecipient>> GetActiveRecipientsAsync(long campaignId, CancellationToken cancellationToken = default)
    {
        return await dbContext.Set<TopUpCampaignRecipient>()
            .AsNoTracking()
            .Where(x => x.TopUpCampaignId == campaignId && x.IsActive)
            .ToListAsync(cancellationToken);
    }
}
