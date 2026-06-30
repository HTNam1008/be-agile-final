using Moe.Application.Abstractions.Clock;
using Moe.Application.Abstractions.Messaging;
using Moe.Application.Abstractions.Persistence;
using Moe.Application.Abstractions.Security;
using Moe.Modules.EducationAccountTopUp.Contracts.TopUps.Enums;
using Moe.Modules.EducationAccountTopUp.Domain.TopUps;
using Moe.Modules.EducationAccountTopUp.IGateway.Repositories;
using Moe.SharedKernel.Results;

namespace Moe.Modules.EducationAccountTopUp.Application.TopUps.ChangeCampaignStatus;

internal sealed class ChangeCampaignStatusCommandHandler(
    ITopUpCampaignRepository campaigns,
    IDynamicTopUpContractRepository contracts,
    IUnitOfWork unitOfWork,
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

        Result statusResult = campaign.ChangeStatus(
            newStatusCode,
            currentUser.UserAccountId ?? 0,
            nowUtc);

        if (statusResult.IsFailure)
        {
            return statusResult;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

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
