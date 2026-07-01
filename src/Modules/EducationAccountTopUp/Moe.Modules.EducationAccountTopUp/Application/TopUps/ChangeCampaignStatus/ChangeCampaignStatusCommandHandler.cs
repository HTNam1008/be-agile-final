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
    IDynamicTopUpContractRepository contracts,
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
        var nowUtc = clock.UtcNow.UtcDateTime;

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

            // Mid-Cycle Freeze: if resuming from a PAUSED state, shift all active contract
            // NextPaymentDates forward by the exact pause duration before re-enabling the run engine.
            // This prevents stale dates from triggering immediate double-billing on resume.
            if (campaign.CampaignStatusCode == TopUpCampaignStatusCodes.Paused)
            {
                TimeSpan? pauseDuration = campaign.RecordResume(nowUtc);
                if (pauseDuration is { TotalSeconds: > 0 })
                {
                    await contracts.ShiftContractPaymentDatesAsync(
                        campaign.Id, pauseDuration.Value, nowUtc, cancellationToken);
                }
            }

            SetNextRunAt(campaign, nowUtc);
        }
        else if (newStatusCode == TopUpCampaignStatusCodes.Paused)
        {
            // Stamp the pause anchor on the campaign domain object.
            // RecordPause also nulls NextRunAtUtc so the scheduler skips this campaign.
            campaign.RecordPause(nowUtc);
        }
        else if (newStatusCode == TopUpCampaignStatusCodes.Cancelled)
        {
            campaign.SetNextRunAt(null);

            // Ick 3 fix: clear PausedAtUtc in case it was paused before cancelling.
            // RecordResume normally handles this, but direct cancel bypasses it.
            if (campaign.CampaignStatusCode == TopUpCampaignStatusCodes.Paused)
            {
                campaign.RecordResume(nowUtc); // returns the duration but we discard it, just clears the anchor
            }

            // Cluster Fix — Orphan Termination: all ACTIVE contracts under a cancelled campaign
            // are perpetual orphans. The run engine will never fire them. Terminate them now
            // so the student ledger correctly shows no further disbursements are expected.
            await contracts.CancelAllActiveContractsAsync(campaign.Id, nowUtc, cancellationToken);
        }

        string oldStatus = campaign.CampaignStatusCode;
        Result statusResult = campaign.ChangeStatus(
            newStatusCode,
            currentUser.UserAccountId ?? 0,
            nowUtc);

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
                $"Top-up Campaign Started: {campaign.CampaignCode}",
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
                "Recurring Top-up Scheduled",
                $"Your next support payment is scheduled for {nextRunAt} (Frequency: {campaign.FrequencyCode ?? "N/A"})."),
            cancellationToken);

        logger.LogInformation(
            "RECURRING_ALERT notification requested for campaign {TopUpCampaignId} and user account {UserAccountId}",
            campaign.Id,
            userAccountId);
    }
}
