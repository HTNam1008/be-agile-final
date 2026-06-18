using Microsoft.EntityFrameworkCore;
using Moe.Application.Abstractions.Clock;
using Moe.Application.Abstractions.Messaging;
using Moe.Application.Abstractions.Security;
using Moe.Modules.EducationAccountTopUp.Contracts.TopUps.Enums;
using Moe.Modules.EducationAccountTopUp.Domain.TopUps;
using Moe.SharedKernel.Results;
using Moe.StudentFinance.Persistence;

namespace Moe.Modules.EducationAccountTopUp.Application.TopUps.ChangeCampaignStatus;

internal sealed class ChangeCampaignStatusCommandHandler(
    MoeDbContext dbContext,
    ICurrentUser currentUser,
    IClock clock) : ICommandHandler<ChangeCampaignStatusCommand>
{
    public async Task<Result> Handle(ChangeCampaignStatusCommand command, CancellationToken cancellationToken)
    {
        var campaign = await dbContext.Set<TopUpCampaign>()
            .FirstOrDefaultAsync(x => x.Id == command.TopUpCampaignId, cancellationToken);

        if (campaign is null)
            return Result.Failure(new Error("NotFound", "Campaign not found."));

        // Cross-Cutting Auth Scope Check
        if (!currentUser.OrganizationUnitIds.Contains(campaign.OrganizationId) && currentUser.OrganizationUnitId != campaign.OrganizationId)
            return Result.Failure(new Error("Forbidden", "User does not have access to the requested OrganizationId."));

        var newStatusCode = Enum.Parse<TopUpCampaignStatusCode>(command.NewStatusCode, ignoreCase: true);

        // Zero-Rule Guard for Activation
        if (newStatusCode == TopUpCampaignStatusCode.Active)
        {
            if (string.Equals(campaign.RecipientModeCode, RecipientModeCode.DynamicRules.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                var ruleCount = await dbContext.Set<TopUpCampaignRule>()
                    .CountAsync(x => x.TopUpCampaignId == campaign.Id && x.IsActive, cancellationToken);

                if (ruleCount == 0)
                    return Result.Failure(new Error("ValidationException", "Cannot activate a DYNAMIC_RULES campaign with zero active rules."));
            }
            else if (string.Equals(campaign.RecipientModeCode, RecipientModeCode.FixedSelection.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                var recipientCount = await dbContext.Set<TopUpCampaignRecipient>()
                    .CountAsync(x => x.TopUpCampaignId == campaign.Id, cancellationToken);

                if (recipientCount == 0)
                    return Result.Failure(new Error("ValidationException", "Cannot activate a FIXED_SELECTION campaign with zero recipients."));
            }
        }

        // Calculate NextRunAt if Activating
        if (newStatusCode == TopUpCampaignStatusCode.Active)
        {
            var scheduleCode = Enum.Parse<ScheduleTypeCode>(campaign.ScheduleTypeCode, ignoreCase: true);
            if (scheduleCode == ScheduleTypeCode.Immediate)
            {
                campaign.SetNextRunAt(clock.UtcNow.UtcDateTime);
            }
            else if (scheduleCode == ScheduleTypeCode.OneTimeScheduled)
            {
                var targetDate = campaign.StartDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
                if (targetDate < clock.UtcNow.UtcDateTime) targetDate = clock.UtcNow.UtcDateTime;
                campaign.SetNextRunAt(targetDate);
            }
            else if (scheduleCode == ScheduleTypeCode.Recurring)
            {
                // Basic logic: if start date is in the future, use it. Otherwise compute based on frequency.
                var targetDate = campaign.StartDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
                if (targetDate < clock.UtcNow.UtcDateTime) targetDate = clock.UtcNow.UtcDateTime; // Simplified for MVP
                campaign.SetNextRunAt(targetDate);
            }
        }
        else if (newStatusCode == TopUpCampaignStatusCode.Paused || newStatusCode == TopUpCampaignStatusCode.Cancelled)
        {
            campaign.SetNextRunAt(null);
        }

        campaign.ChangeStatus(newStatusCode.ToString(), currentUser.UserAccountId ?? 0, clock.UtcNow.UtcDateTime);

        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
