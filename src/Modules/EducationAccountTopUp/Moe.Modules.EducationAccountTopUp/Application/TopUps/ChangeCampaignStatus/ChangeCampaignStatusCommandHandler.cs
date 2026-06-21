using Moe.Application.Abstractions.Clock;
using Moe.Application.Abstractions.Messaging;
using Moe.Application.Abstractions.Security;
using Moe.Modules.EducationAccountTopUp.Contracts.TopUps.Enums;
using Moe.Modules.EducationAccountTopUp.Domain.TopUps;
using Moe.Modules.EducationAccountTopUp.IGateway.Repositories;
using Moe.SharedKernel.Results;

namespace Moe.Modules.EducationAccountTopUp.Application.TopUps.ChangeCampaignStatus;

internal sealed class ChangeCampaignStatusCommandHandler(
    ITopUpCampaignRepository campaigns,
    ICurrentUser currentUser,
    IAdminAccessControl adminAccess,
    IClock clock) : ICommandHandler<ChangeCampaignStatusCommand>
{
    public async Task<Result> Handle(ChangeCampaignStatusCommand command, CancellationToken cancellationToken)
    {
        TopUpCampaign? campaign = await campaigns.GetByIdAsync(command.TopUpCampaignId, cancellationToken);

        if (campaign is null)
        {
            return Result.Failure(TopUpErrors.CampaignNotFound);
        }

        Result access = adminAccess.EnsureCanAccessOrganization(campaign.OrganizationId);
        if (access.IsFailure)
        {
            return Result.Failure(TopUpErrors.OrganizationOutsideScope);
        }

        var newStatusCode = command.NewStatusCode.ToUpperInvariant();

        if (newStatusCode == TopUpCampaignStatusCodes.Active)
        {
            if (string.Equals(campaign.RecipientModeCode, RecipientModeCode.DynamicRules.ToString(), StringComparison.OrdinalIgnoreCase)
                && await campaigns.CountActiveRulesAsync(campaign.Id, cancellationToken) == 0)
            {
                return Result.Failure(new Error("ValidationException", "Cannot activate a DYNAMIC_RULES campaign with zero active rules."));
            }

            if (string.Equals(campaign.RecipientModeCode, RecipientModeCode.FixedSelection.ToString(), StringComparison.OrdinalIgnoreCase)
                && await campaigns.CountActiveRecipientsAsync(campaign.Id, cancellationToken) == 0)
            {
                return Result.Failure(new Error("ValidationException", "Cannot activate a FIXED_SELECTION campaign with zero recipients."));
            }

            SetNextRunAt(campaign, clock.UtcNow.UtcDateTime);
        }
        else if (newStatusCode == TopUpCampaignStatusCodes.Paused || newStatusCode == TopUpCampaignStatusCodes.Cancelled)
        {
            campaign.SetNextRunAt(null);
        }

        campaign.ChangeStatus(
            newStatusCode,
            currentUser.UserAccountId ?? 0,
            clock.UtcNow.UtcDateTime);



        return Result.Success();
    }

    private static void SetNextRunAt(TopUpCampaign campaign, DateTime utcNow)
    {
        var scheduleCode = Enum.Parse<ScheduleTypeCode>(campaign.ScheduleTypeCode, ignoreCase: true);
        if (scheduleCode == ScheduleTypeCode.Immediate)
        {
            campaign.SetNextRunAt(utcNow);
            return;
        }

        DateTime targetDate = campaign.StartDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        campaign.SetNextRunAt(targetDate < utcNow ? utcNow : targetDate);
    }
}
