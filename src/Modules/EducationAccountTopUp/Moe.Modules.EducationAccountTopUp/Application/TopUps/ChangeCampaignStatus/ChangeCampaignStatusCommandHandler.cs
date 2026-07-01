using Moe.Application.Abstractions.Audit;
using Moe.Application.Abstractions.Clock;
using Moe.Application.Abstractions.Messaging;
using Moe.Application.Abstractions.Persistence;
using Moe.Application.Abstractions.Security;
using Moe.Modules.EducationAccountTopUp.Contracts.TopUps.Enums;
using Moe.Modules.EducationAccountTopUp.Domain.TopUps;
using Moe.Modules.EducationAccountTopUp.IGateway.Repositories;
using Moe.Modules.Notifications.Domain.Notifications;
using Moe.Modules.Notifications.IGateway.Notifications;
using Moe.SharedKernel.Results;
using Microsoft.Extensions.Logging;

namespace Moe.Modules.EducationAccountTopUp.Application.TopUps.ChangeCampaignStatus;

internal sealed class ChangeCampaignStatusCommandHandler(
    ITopUpCampaignRepository campaigns,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IAdminAccessControl adminAccess,
    IClock clock,
    IAuditService audit,
    INotificationWriter notificationWriter,
    ILogger<ChangeCampaignStatusCommandHandler> logger) : ICommandHandler<ChangeCampaignStatusCommand>
{
    public async Task<Result> Handle(ChangeCampaignStatusCommand command, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "ChangeCampaignStatus started for campaign {TopUpCampaignId} with requested status {NewStatusCode}",
            command.TopUpCampaignId,
            command.NewStatusCode);

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
                return Result.Failure(TopUpErrors.EmptyDynamicRules);
            }

            if (string.Equals(campaign.RecipientModeCode, RecipientModeCode.FixedSelection.ToString(), StringComparison.OrdinalIgnoreCase)
                && await campaigns.CountActiveRecipientsAsync(campaign.Id, cancellationToken) == 0)
            {
                return Result.Failure(TopUpErrors.EmptyFixedRecipients);
            }

            SetNextRunAt(campaign, clock.UtcNow.UtcDateTime);
        }
        else if (newStatusCode == TopUpCampaignStatusCodes.Paused || newStatusCode == TopUpCampaignStatusCodes.Cancelled)
        {
            campaign.SetNextRunAt(null);
        }

        string oldStatus = campaign.CampaignStatusCode;
        Result statusResult = campaign.ChangeStatus(
            newStatusCode,
            currentUser.UserAccountId ?? 0,
            clock.UtcNow.UtcDateTime);

        if (statusResult.IsFailure)
        {
            return statusResult;
        }

        await audit.RecordSchoolActionAsync(
            new SchoolAuditContext(
                AuditActionCodes.TopUpCampaignStatusChanged,
                "TopUpCampaign",
                campaign.Id,
                campaign.OrganizationId,
                new SchoolAuditDetails(
                    "Top-up campaign status changed",
                    EntityDisplayName: campaign.CampaignName,
                    StatusTransition: new SchoolAuditStatusTransition(oldStatus, campaign.CampaignStatusCode))),
            cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        if (newStatusCode == TopUpCampaignStatusCodes.Active && currentUser.UserAccountId is long userAccountId)
        {
            logger.LogInformation(
                "Campaign {TopUpCampaignId} saved successfully. Preparing activation notifications for user account {UserAccountId}",
                campaign.Id,
                userAccountId);

            await NotifyCampaignLaunchAsync(campaign, userAccountId, cancellationToken);

            if (string.Equals(campaign.ScheduleTypeCode, ScheduleTypeCode.Recurring.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                await NotifyRecurringAlertAsync(campaign, userAccountId, cancellationToken);
            }
        }

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

    private async Task NotifyCampaignLaunchAsync(
        TopUpCampaign campaign,
        long userAccountId,
        CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Creating CAMPAIGN_LAUNCH notification for campaign {TopUpCampaignId} and user account {UserAccountId}",
            campaign.Id,
            userAccountId);

        await notificationWriter.CreateAsync(
            new NotificationCreateRequest(
                userAccountId,
                NotificationTypeCode.CampaignLaunch,
                $"New Support Campaign: {campaign.CampaignCode}",
                $"{campaign.CampaignName} has been launched for eligible students."),
            cancellationToken);

        logger.LogInformation(
            "CAMPAIGN_LAUNCH notification requested for campaign {TopUpCampaignId} and user account {UserAccountId}",
            campaign.Id,
            userAccountId);
    }

    private async Task NotifyRecurringAlertAsync(
        TopUpCampaign campaign,
        long userAccountId,
        CancellationToken cancellationToken)
    {
        string nextRunAt = campaign.NextRunAtUtc?.ToString("yyyy-MM-dd HH:mm") ?? "TBD";

        logger.LogInformation(
            "Creating RECURRING_ALERT notification for campaign {TopUpCampaignId} and user account {UserAccountId}. NextRunAt={NextRunAt}, FrequencyCode={FrequencyCode}",
            campaign.Id,
            userAccountId,
            nextRunAt,
            campaign.FrequencyCode ?? "N/A");

        await notificationWriter.CreateAsync(
            new NotificationCreateRequest(
                userAccountId,
                NotificationTypeCode.RecurringAlert,
                "Recurring Allowance Set",
                $"Your next support payment is scheduled for {nextRunAt} (Frequency: {campaign.FrequencyCode ?? "N/A"})."),
            cancellationToken);

        logger.LogInformation(
            "RECURRING_ALERT notification requested for campaign {TopUpCampaignId} and user account {UserAccountId}",
            campaign.Id,
            userAccountId);
    }
}
