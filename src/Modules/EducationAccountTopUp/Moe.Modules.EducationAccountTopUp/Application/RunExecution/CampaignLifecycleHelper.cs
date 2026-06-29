using Moe.Modules.EducationAccountTopUp.Domain.TopUps;
using Moe.Modules.EducationAccountTopUp.IGateway.Repositories;

namespace Moe.Modules.EducationAccountTopUp.Application.RunExecution;

internal static class CampaignLifecycleHelper
{
    public static async Task EvaluateCampaignAfterTerminalRunAsync(
        TopUpRun run,
        ITopUpCampaignRepository campaigns,
        IDynamicTopUpContractRepository contracts,
        DateTime completedAtUtc,
        CancellationToken cancellationToken)
    {
        TopUpCampaign? campaign = await campaigns.GetByIdAsync(run.TopUpCampaignId, cancellationToken);
        if (campaign is not null)
        {
            bool shouldComplete =
                campaign.ScheduleTypeCode == ScheduleTypes.Immediate ||
                campaign.ScheduleTypeCode == ScheduleTypes.OneTimeScheduled ||
                (campaign.ScheduleTypeCode == ScheduleTypes.Recurring && campaign.NextRunAtUtc == null);

            if (shouldComplete)
            {
                campaign.ChangeStatus(TopUpCampaignStatusCodes.Completed, 0, completedAtUtc, true);

                // Terminate orphaned ACTIVE contracts — but ONLY for delivery types that do NOT
                // survive campaign expiry. Per the core product spec (Pillar 1, Case 2):
                //   "The campaign expiry only closes the door to new applicants; it never breaks
                //    an existing promise [for FixedContract students]."
                // Therefore:
                //   - INSTANT / CONDITIONAL_RECURRING: hard-stop on campaign completion.
                //   - FIXED_CONTRACT: contracts survive until their personal cap is reached.
                //     (NOTE: these orphaned FixedContract contracts need a dedicated scheduler
                //      to fire them — see architectural gap in product backlog.)
                await contracts.CancelNonFixedContractActiveContractsAsync(campaign.Id, completedAtUtc, cancellationToken);
            }
        }
    }
}
