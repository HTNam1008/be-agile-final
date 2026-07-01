using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moe.Application.Abstractions.Clock;
using Moe.Application.Abstractions.Persistence;
using Moe.Modules.EducationAccountTopUp.Contracts.TopUps.Enums;
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
                await PollAndScheduleRunsAsync(stoppingToken);
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

    private async Task PollAndScheduleRunsAsync(CancellationToken cancellationToken)
    {
        DateTime nowUtc = clock.UtcNow.UtcDateTime;

        using IServiceScope scope = scopeFactory.CreateScope();
        ITopUpCampaignRepository campaigns = scope.ServiceProvider.GetRequiredService<ITopUpCampaignRepository>();
        var dueCampaigns = await campaigns.GetDueCampaignsAsync(nowUtc, cancellationToken);
        var dueCampaignIds = dueCampaigns.Select(c => c.Id).ToList();

        if (dueCampaignIds.Count > 0)
        {
            logger.LogInformation("Found {Count} campaigns due for execution.", dueCampaignIds.Count);
        }

        foreach (long campaignId in dueCampaignIds)
        {
            using IServiceScope innerScope = scopeFactory.CreateScope();
            IDistributedLock distributedLock = innerScope.ServiceProvider.GetRequiredService<IDistributedLock>();
            string lockKey = $"topup-scheduler:campaign:{campaignId}";
            bool lockAcquired = false;

            try
            {
                lockAcquired = await distributedLock.TryAcquireAsync(lockKey, TimeSpan.FromMinutes(2), cancellationToken);
                if (!lockAcquired)
                {
                    logger.LogInformation("Skipping campaign {CampaignId} — another instance is already scheduling it.", campaignId);
                    continue;
                }

                ITopUpCampaignRepository campaignRepo = innerScope.ServiceProvider.GetRequiredService<ITopUpCampaignRepository>();
                ITopUpRunRepository runs = innerScope.ServiceProvider.GetRequiredService<ITopUpRunRepository>();
                IUnitOfWork unitOfWork = innerScope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                ITopUpRunDispatcher dispatcher = innerScope.ServiceProvider.GetRequiredService<ITopUpRunDispatcher>();

                TopUpCampaign? campaign = await campaignRepo.GetByIdAsync(campaignId, cancellationToken);
                if (campaign is null || !campaign.NextRunAtUtc.HasValue) continue;

                if (string.Equals(campaign.RecipientModeCode, RecipientModeCode.DynamicRules.ToString(), StringComparison.OrdinalIgnoreCase))
                {
                    logger.LogWarning("Campaign {CampaignId} is DYNAMIC_RULES; scheduled disbursements are handled by the assessment and contract workers.", campaign.Id);
                    continue;
                }

                if (string.Equals(campaign.RecipientModeCode, RecipientModeCode.FixedSelection.ToString(), StringComparison.OrdinalIgnoreCase))
                {
                    int count = await campaignRepo.CountActiveRecipientsAsync(campaign.Id, cancellationToken);
                    if (count == 0)
                    {
                        logger.LogWarning("Skipping fixed campaign {CampaignId} — 0 recipients", campaign.Id);
                        continue;
                    }
                }

                DateTime scheduledFor = campaign.NextRunAtUtc.Value;

                bool runExists = await runs.ExistsForScheduledOccurrenceAsync(campaign.Id, scheduledFor, cancellationToken);
                if (runExists)
                {
                    logger.LogWarning("Run for campaign {CampaignId} scheduled at {ScheduledFor} already exists.", campaign.Id, scheduledFor);
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

                logger.LogInformation("Successfully scheduled and dispatched run {RunId} for campaign {CampaignId}.", run.Id, campaign.Id);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to schedule run for campaign {CampaignId}", campaignId);
            }
            finally
            {
                if (lockAcquired)
                {
                    await distributedLock.ReleaseAsync(lockKey);
                }
            }
        }

        await DispatchContractRunsAsync(nowUtc, cancellationToken);
    }

    private async Task DispatchContractRunsAsync(DateTime nowUtc, CancellationToken ct)
    {
        using IServiceScope scope = scopeFactory.CreateScope();
        var contractRepo = scope.ServiceProvider.GetRequiredService<IDynamicTopUpContractRepository>();
        var campaignRepo = scope.ServiceProvider.GetRequiredService<ITopUpCampaignRepository>();
        var runs = scope.ServiceProvider.GetRequiredService<ITopUpRunRepository>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var dispatcher = scope.ServiceProvider.GetRequiredService<ITopUpRunDispatcher>();

        var dueContracts = await contractRepo.GetDueForPaymentAsync(nowUtc, ct);
        if (dueContracts.Count == 0) return;

        var byCampaign = dueContracts.GroupBy(c => c.TopUpCampaignId);

        var distributedLock = scope.ServiceProvider.GetRequiredService<IDistributedLock>();

        foreach (var group in byCampaign)
        {
            string lockKey = $"topup-scheduler:contract-run:{group.Key}";
            bool lockAcquired = await distributedLock.TryAcquireAsync(lockKey, TimeSpan.FromMinutes(2), ct);
            if (!lockAcquired)
            {
                logger.LogInformation("Skipping contract runs for campaign {CampaignId} — another instance is already scheduling it.", group.Key);
                continue;
            }

            try
            {
                var campaign = await campaignRepo.GetByIdAsync(group.Key, ct);
                if (campaign is null) continue;

                bool activeRunExists = await runs.HasActiveRunsForCampaignAsync(group.Key, ct);
                if (activeRunExists)
                {
                    logger.LogInformation("Skipping contract dispatch for campaign {CampaignId} — an active run is already processing.", group.Key);
                    continue;
                }

                var run = TopUpRun.CreateForContracts(group.Key, campaign.CampaignVersion, nowUtc);
                await runs.AddAsync(run, ct);
                await unitOfWork.SaveChangesAsync(ct);
                await dispatcher.EnqueueAsync(run.Id, ct);

                logger.LogInformation(
                    "Dispatched contract-driven run {RunId} for campaign {CampaignId} with {ContractCount} due contracts.",
                    run.Id, group.Key, group.Count());
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to dispatch contract-driven run for campaign {CampaignId}", group.Key);
            }
            finally
            {
                if (lockAcquired)
                {
                    await distributedLock.ReleaseAsync(lockKey);
                }
            }
        }
    }
}
