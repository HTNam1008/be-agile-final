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
            if (campaign.ScheduleTypeCode == ScheduleTypes.Immediate ||
                campaign.ScheduleTypeCode == ScheduleTypes.OneTimeScheduled)
            {
                campaign.ChangeStatus(TopUpCampaignStatusCodes.Completed, 0, completedAtUtc, true);
            }
            else if (campaign.ScheduleTypeCode == ScheduleTypes.Recurring && campaign.NextRunAtUtc == null)
            {
                campaign.ChangeStatus(TopUpCampaignStatusCodes.Completed, 0, completedAtUtc, true);
            }
        }
    }
}
