using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moe.Application.Abstractions.Clock;
using Moe.Application.Abstractions.Persistence;
using Moe.Modules.EducationAccountTopUp.Domain.TopUps;
using Moe.Modules.EducationAccountTopUp.IGateway;
using Moe.Modules.EducationAccountTopUp.IGateway.Repositories;

namespace Moe.Modules.EducationAccountTopUp.Application.RunExecution;

public sealed class TopUpSchedulerWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<TopUpSchedulerWorker> logger,
    IClock clock) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Top-up scheduler worker started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "Unhandled error while polling for scheduled top-up runs.");
            }

            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }

    public async Task<TopUpSchedulerRunOnceResult> RunOnceAsync(CancellationToken cancellationToken)
    {
        DateTime nowUtc = clock.UtcNow.UtcDateTime;
        IReadOnlyList<long> dueCampaignIds;

        using (IServiceScope scope = scopeFactory.CreateScope())
        {
            ITopUpCampaignRepository campaigns = scope.ServiceProvider.GetRequiredService<ITopUpCampaignRepository>();
            var dueCampaigns = await campaigns.GetDueCampaignsAsync(nowUtc, cancellationToken);
            dueCampaignIds = dueCampaigns.Select(c => c.Id).ToList();
        }

        if (dueCampaignIds.Count > 0)
        {
            logger.LogInformation("Found {Count} campaigns due for execution.", dueCampaignIds.Count);
        }

        int createdRunCount = 0;
        int skippedRunCount = 0;
        int failedRunCount = 0;
        List<long> createdRunIds = [];

        foreach (long campaignId in dueCampaignIds)
        {
            try
            {
                using IServiceScope innerScope = scopeFactory.CreateScope();
                ITopUpCampaignRepository campaigns = innerScope.ServiceProvider.GetRequiredService<ITopUpCampaignRepository>();
                ITopUpRunRepository runs = innerScope.ServiceProvider.GetRequiredService<ITopUpRunRepository>();
                IUnitOfWork unitOfWork = innerScope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                ITopUpRunDispatcher dispatcher = innerScope.ServiceProvider.GetRequiredService<ITopUpRunDispatcher>();

                TopUpCampaign? campaign = await campaigns.GetByIdAsync(campaignId, cancellationToken);
                if (campaign is null || !campaign.NextRunAtUtc.HasValue)
                {
                    skippedRunCount++;
                    continue;
                }

                DateTime scheduledFor = campaign.NextRunAtUtc.Value;

                bool runExists = await runs.ExistsForScheduledOccurrenceAsync(campaign.Id, scheduledFor, cancellationToken);
                if (runExists)
                {
                    logger.LogWarning("Run for campaign {CampaignId} scheduled at {ScheduledFor} already exists.", campaign.Id, scheduledFor);
                    skippedRunCount++;
                    continue;
                }

                TopUpRun run = TopUpRun.CreateScheduled(
                    campaign.Id,
                    campaign.CampaignVersion,
                    scheduledFor,
                    $"SYS-{campaign.Id}-{scheduledFor:yyyyMMddHHmmss}",
                    null,
                    nowUtc);

                await runs.AddAsync(run, cancellationToken);

                DateTime? nextRun = RecurrenceCalculator.CalculateNextRun(
                    campaign.FrequencyCode ?? "",
                    campaign.FrequencyInterval ?? 1,
                    scheduledFor,
                    campaign.EndDate);

                campaign.SetNextRunAt(nextRun);

                await unitOfWork.SaveChangesAsync(cancellationToken);
                await dispatcher.EnqueueAsync(run.Id, cancellationToken);

                createdRunCount++;
                createdRunIds.Add(run.Id);
                logger.LogInformation("Successfully scheduled and dispatched run {RunId} for campaign {CampaignId}.", run.Id, campaign.Id);
            }
            catch (Exception ex)
            {
                failedRunCount++;
                logger.LogError(ex, "Failed to schedule run for campaign {CampaignId}", campaignId);
            }
        }

        return new TopUpSchedulerRunOnceResult(
            dueCampaignIds.Count,
            createdRunCount,
            skippedRunCount,
            failedRunCount,
            createdRunIds);
    }
}

public sealed record TopUpSchedulerRunOnceResult(
    int DueCampaignCount,
    int CreatedRunCount,
    int SkippedRunCount,
    int FailedRunCount,
    IReadOnlyList<long> CreatedRunIds);
