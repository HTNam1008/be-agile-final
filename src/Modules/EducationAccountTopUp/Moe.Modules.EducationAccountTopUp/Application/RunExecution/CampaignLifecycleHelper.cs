using Moe.Modules.EducationAccountTopUp.Domain.TopUps;
using Moe.Modules.EducationAccountTopUp.IGateway.Repositories;

namespace Moe.Modules.EducationAccountTopUp.Application.RunExecution;

internal static class CampaignLifecycleHelper
{
    public static async Task EvaluateCampaignAfterTerminalRunAsync(
        TopUpRun run,
        ITopUpCampaignRepository campaigns,
        DateTime completedAtUtc,
        CancellationToken cancellationToken)
    {
        TopUpCampaign? campaign = await campaigns.GetByIdAsync(run.TopUpCampaignId, cancellationToken);
        if (campaign is not null)
        {
            if (string.Equals(campaign.ScheduleTypeCode, "IMMEDIATE", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(campaign.ScheduleTypeCode, "ONETIME_SCHEDULED", StringComparison.OrdinalIgnoreCase))
            {
                campaign.ChangeStatus(TopUpCampaignStatusCodes.Completed, 0, completedAtUtc, true);
            }
            else if (string.Equals(campaign.ScheduleTypeCode, "RECURRING", StringComparison.OrdinalIgnoreCase) && campaign.NextRunAtUtc == null)
            {
                campaign.ChangeStatus(TopUpCampaignStatusCodes.Completed, 0, completedAtUtc, true);
            }
        }
    }
}
