using Microsoft.EntityFrameworkCore;
using Moe.Modules.EducationAccountTopUp.Domain.TopUps;
using Moe.Modules.EducationAccountTopUp.IGateway.RunSummary;
using Moe.StudentFinance.Persistence;

namespace Moe.Modules.EducationAccountTopUp.Infrastructure.RunSummary;

internal sealed class TopUpRunSummaryReader(MoeDbContext dbContext)
    : ITopUpRunSummaryReader
{
    public Task<RunSummaryProjection?> GetByIdAsync(
        long runId,
        CancellationToken cancellationToken)
    {
        IQueryable<TopUpRun> runs = dbContext.Set<TopUpRun>().AsNoTracking();
        IQueryable<TopUpCampaign> campaigns =
            dbContext.Set<TopUpCampaign>().AsNoTracking();

        return (
            from run in runs
            join campaign in campaigns
                on run.TopUpCampaignId equals campaign.Id
            where run.Id == runId
            select new RunSummaryProjection(
                run.Id,
                run.TopUpCampaignId,
                campaign.CampaignCode,
                campaign.CampaignName,
                campaign.OrganizationId,
                campaign.MaxTotalAmount,
                run.ScheduledForUtc,
                run.TriggerTypeCode,
                run.RunStatusCode,
                run.TotalSelected,
                run.TotalProcessed,
                run.TotalSucceeded,
                run.TotalFailed,
                run.TotalSkipped,
                run.TotalAmount,
                run.StartedAtUtc,
                run.CompletedAtUtc))
            .SingleOrDefaultAsync(cancellationToken);
    }
}
