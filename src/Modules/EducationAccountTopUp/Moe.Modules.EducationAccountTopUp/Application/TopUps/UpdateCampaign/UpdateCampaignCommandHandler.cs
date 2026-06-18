using Microsoft.EntityFrameworkCore;
using Moe.Application.Abstractions.Clock;
using Moe.Application.Abstractions.Messaging;
using Moe.Application.Abstractions.Security;
using Moe.Modules.EducationAccountTopUp.Contracts.TopUps.Enums;
using Moe.Modules.EducationAccountTopUp.Domain.TopUps;
using Moe.SharedKernel.Results;
using Moe.StudentFinance.Persistence;

namespace Moe.Modules.EducationAccountTopUp.Application.TopUps.UpdateCampaign;

internal sealed class UpdateCampaignCommandHandler(
    MoeDbContext dbContext,
    ICurrentUser currentUser,
    IClock clock) : ICommandHandler<UpdateCampaignCommand>
{
    public async Task<Result> Handle(UpdateCampaignCommand command, CancellationToken cancellationToken)
    {
        var campaign = await dbContext.Set<TopUpCampaign>()
            .FirstOrDefaultAsync(x => x.Id == command.TopUpCampaignId, cancellationToken);

        if (campaign is null)
            return Result.Failure(new Error("NotFound", "Campaign not found."));

        // Cross-Cutting Auth Scope Check
        if (!currentUser.OrganizationUnitIds.Contains(campaign.OrganizationId) && currentUser.OrganizationUnitId != campaign.OrganizationId)
            return Result.Failure(new Error("Forbidden", "User does not have access to the requested OrganizationId."));

        // Validation constraint: Cannot update unless DRAFT or PAUSED. 
        if (campaign.CampaignStatusCode != TopUpCampaignStatusCode.Draft.ToString() &&
            campaign.CampaignStatusCode != TopUpCampaignStatusCode.Paused.ToString())
        {
            return Result.Failure(new Error("InvalidStatus", "Only DRAFT or PAUSED campaigns can be modified."));
        }

        // Concurrency check based on version
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
            nowUtc: clock.UtcNow.UtcDateTime
        );

        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
