using Moe.Application.Abstractions.Clock;
using Moe.Application.Abstractions.Messaging;
using Moe.Application.Abstractions.Security;
using Moe.Modules.EducationAccountTopUp.Contracts.TopUps.Enums;
using Moe.Modules.EducationAccountTopUp.Domain.TopUps;
using Moe.SharedKernel.Results;
using Moe.StudentFinance.Persistence;

namespace Moe.Modules.EducationAccountTopUp.Application.TopUps.CreateCampaign;

internal sealed class CreateCampaignCommandHandler(
    MoeDbContext dbContext,
    ICurrentUser currentUser,
    IClock clock) : ICommandHandler<CreateCampaignCommand, long>
{
    public async Task<Result<long>> Handle(CreateCampaignCommand command, CancellationToken cancellationToken)
    {
        var request = command.Request;

        // Cross-Cutting Auth Scope Check
        if (!currentUser.OrganizationUnitIds.Contains(request.OrganizationId) && currentUser.OrganizationUnitId != request.OrganizationId)
        {
            return Result<long>.Failure(new Error("Forbidden", "User does not have access to the requested OrganizationId."));
        }

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

        var campaign = TopUpCampaign.Create(
            organizationId: request.OrganizationId,
            campaignCode: request.CampaignCode,
            campaignName: request.CampaignName,
            description: request.Description,
            recipientModeCode: request.RecipientModeCode,
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

        dbContext.Set<TopUpCampaign>().Add(campaign);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result<long>.Success(campaign.Id);
    }
}
