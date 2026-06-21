using Moe.Application.Abstractions.Clock;
using Moe.Application.Abstractions.Messaging;
using Moe.Application.Abstractions.Security;
using Moe.Modules.EducationAccountTopUp.Contracts.TopUps.Enums;
using Moe.Modules.EducationAccountTopUp.Domain.TopUps;
using Moe.Modules.EducationAccountTopUp.IGateway.Repositories;
using Moe.SharedKernel.Results;

namespace Moe.Modules.EducationAccountTopUp.Application.TopUps.UpdateCampaign;

internal sealed class UpdateCampaignCommandHandler(
    ITopUpCampaignRepository campaigns,
    ICurrentUser currentUser,
    IAdminAccessControl adminAccess,
    IClock clock) : ICommandHandler<UpdateCampaignCommand>
{
    public async Task<Result> Handle(UpdateCampaignCommand command, CancellationToken cancellationToken)
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

        if (campaign.CampaignStatusCode != TopUpCampaignStatusCodes.Draft
            && campaign.CampaignStatusCode != TopUpCampaignStatusCodes.Paused)
        {
            return Result.Failure(TopUpErrors.InvalidCampaignStatus);
        }

        if (campaign.CampaignVersion != command.Request.CampaignVersion)
        {
            return Result.Failure(new Error("ConcurrencyException", "The campaign has been modified by another process."));
        }

        var request = command.Request;
        var scheduleTypeCode = Enum.Parse<ScheduleTypeCode>(request.ScheduleTypeCode, ignoreCase: true);

        string? frequencyCode = null;
        int? frequencyInterval = null;
        DateOnly? endDate = null;

        if (scheduleTypeCode == ScheduleTypeCode.Recurring)
        {
            frequencyCode = request.FrequencyCode;
            frequencyInterval = request.FrequencyInterval;
            endDate = request.EndDate;
        }

        campaign.Update(
            campaignName: request.CampaignName,
            description: request.Description,
            defaultTopUpAmount: request.DefaultTopUpAmount,
            reason: request.Reason,
            scheduleTypeCode: request.ScheduleTypeCode,
            startDate: request.StartDate,
            endDate: endDate,
            frequencyCode: frequencyCode,
            frequencyInterval: frequencyInterval,
            currentUserId: currentUser.UserAccountId ?? 0,
            nowUtc: clock.UtcNow.UtcDateTime);



        return Result.Success();
    }
}
