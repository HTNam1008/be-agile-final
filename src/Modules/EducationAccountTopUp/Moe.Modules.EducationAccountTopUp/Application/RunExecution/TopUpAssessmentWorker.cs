using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moe.Application.Abstractions.Clock;
using Moe.Application.Abstractions.Persistence;
using Moe.Modules.EducationAccountTopUp.Domain.TopUps;
using Moe.Modules.EducationAccountTopUp.IGateway.Repositories;
using Moe.Modules.EducationAccountTopUp.IGateway.TopUps;
using CampaignRuleProjection = Moe.Modules.EducationAccountTopUp.IGateway.TopUps.CampaignRuleProjection;

namespace Moe.Modules.EducationAccountTopUp.Application.RunExecution;

public sealed class TopUpAssessmentWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<TopUpAssessmentWorker> logger,
    IClock clock,
    IOptions<TopUpWorkerOptions> options) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Top-up assessment worker started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunAssessmentAsync(stoppingToken);
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Unhandled error in assessment cycle.");
            }

            DateTime now = clock.UtcNow.UtcDateTime;
            DateTime nextRun = now.Date.AddDays(1); // Midnight tomorrow
            TimeSpan delay = nextRun - now;

            logger.LogInformation("Assessment cycle completed. Next run at {NextRunUtc} (in {DelayHours:N1} hours).", nextRun, delay.TotalHours);
            await Task.Delay(delay, stoppingToken);
        }
    }

    private async Task RunAssessmentAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var campaignRepo = scope.ServiceProvider.GetRequiredService<ITopUpCampaignRepository>();
        var campaignReader = scope.ServiceProvider.GetRequiredService<ITopUpCampaignReader>();
        var ruleFilter = scope.ServiceProvider.GetRequiredService<IDynamicRuleFilter>();
        var contractRepo = scope.ServiceProvider.GetRequiredService<IDynamicTopUpContractRepository>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var distributedLock = scope.ServiceProvider.GetRequiredService<IDistributedLock>();

        DateTime nowUtc = clock.UtcNow.UtcDateTime;
        DateOnly today = DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);
        var activeCampaigns = await campaignRepo.GetDueForAssessmentAsync(today, ct);

        foreach (var campaign in activeCampaigns)
        {
            string lockKey = $"topup-assessment:campaign:{campaign.Id}";
            bool lockAcquired = false;

            try
            {
                lockAcquired = await distributedLock.TryAcquireAsync(lockKey, options.Value.AssessmentLockTtl, ct);
                if (!lockAcquired)
                {
                    logger.LogWarning("Skipping assessment for campaign {CampaignId} — lock acquisition failed. Another instance may be processing or a previous run timed out.", campaign.Id);
                    continue;
                }

                var rules = await campaignReader.GetRulesAsync(campaign.Id, ct);
                int totalCount = await ruleFilter.CountMatchingAccountsAsync(rules, nowUtc, ct);

                var allQualifyingIds = new List<long>(totalCount);

                const int pageSize = 5000;
                int totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

                for (int i = 0; i < totalPages; i++)
                {
                    var chunkIds = await ruleFilter.FilterAccountIdsAsync(rules, i * pageSize, pageSize, nowUtc, ct);
                    allQualifyingIds.AddRange(chunkIds);
                    var chunkSet = chunkIds.ToHashSet();

                    // Ick 1 fix: check for ALL existing contracts, not just ACTIVE ones.
                    // If a student got an INSTANT contract, got paid, and it became COMPLETED,
                    // they still have an existing contract and should not get a second one.
                    var existingContracts = await contractRepo.GetByCampaignAndAccountsAsync(campaign.Id, chunkIds, ct);
                    var existingByAccount = existingContracts
                        .GroupBy(c => c.EducationAccountId)
                        .ToDictionary(g => g.Key, g => g.First());

                    foreach (long accountId in chunkSet)
                    {
                        if (existingByAccount.TryGetValue(accountId, out var existing))
                        {
                            if (campaign.DeliveryTypeCode == DeliveryType.ConditionalRecurring
                                && (!existing.NextPaymentDate.HasValue || existing.NextPaymentDate.Value <= nowUtc))
                            {
                                existing.SetNextPaymentDate(
                                    RecurrenceCalculator.CalculateNextRun(
                                        campaign.FrequencyCode ?? "MONTHLY",
                                        campaign.FrequencyInterval ?? 1,
                                        nowUtc,
                                        campaign.EndDate) ?? nowUtc,
                                    nowUtc);
                            }
                        }
                        else
                        {
                            DateTime nextPaymentDate = nowUtc;

                            var contract = DynamicTopUpContract.Create(
                                campaign.Id, accountId,
                                campaign.DeliveryTypeCode,
                                campaign.DefaultTopUpAmount,
                                campaign.MaxTotalAmount > 0 ? campaign.MaxTotalAmount : campaign.DefaultTopUpAmount,
                                campaign.FrequencyCode ?? "MONTHLY",
                                campaign.FrequencyInterval ?? 1,
                                nowUtc,
                                nextPaymentDate);

                            await contractRepo.AddAsync(contract, ct);
                        }
                    }

                    await unitOfWork.SaveChangesAsync(ct);
                }

                // Suspend any active contracts that no longer qualify (using bulk update)
                await contractRepo.SuspendNonQualifyingContractsAsync(campaign.Id, allQualifyingIds, nowUtc, ct);

                await unitOfWork.SaveChangesAsync(ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "Assessment failed for campaign {CampaignId} — continuing with next campaign",
                    campaign.Id);
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
