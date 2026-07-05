using Moe.Application.Abstractions.Audit;
using Moe.Application.Abstractions.Clock;
using Moe.Application.Abstractions.Messaging;
using Moe.Application.Abstractions.Persistence;
using Moe.Application.Abstractions.Security;
using Moe.Modules.EducationAccountTopUp.Contracts.TopUps.Enums;
using Moe.Modules.EducationAccountTopUp.Domain.TopUps;
using Moe.Modules.EducationAccountTopUp.IGateway.Repositories;
using Moe.SharedKernel.Results;

namespace Moe.Modules.EducationAccountTopUp.Application.TopUps.CreateCampaign;

internal sealed class CreateCampaignCommandHandler(
    ITopUpCampaignRepository campaigns,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IAdminAccessControl adminAccess,
    IClock clock,
    IAuditService audit) : ICommandHandler<CreateCampaignCommand, long>
{
    public async Task<Result<long>> Handle(CreateCampaignCommand command, CancellationToken cancellationToken)
    {
        var request = command.Request;

        Result access = adminAccess.EnsureCanAccessOrganization(request.OrganizationId);
        if (access.IsFailure)
            return Result<long>.Failure(TopUpErrors.OrganizationOutsideScope);

        if (await campaigns.CampaignCodeExistsAsync(request.OrganizationId, request.CampaignCode, cancellationToken))
        {
            return Result<long>.Failure(new Error(
                "TopUpCampaign.DuplicateCampaignCode",
                $"A campaign with code '{request.CampaignCode}' already exists for this organisation."));
        }

        var scheduleTypeCode = Enum.Parse<ScheduleTypeCode>(request.ScheduleTypeCode, ignoreCase: true);

        string? frequencyCode = null;
        int? frequencyInterval = null;
        int? weeklyDayOfWeek = null;
        int? monthlyDay = null;
        DateOnly? endDate = null;

        if (scheduleTypeCode == ScheduleTypeCode.Recurring ||
            request.DeliveryTypeCode == DeliveryType.FixedContract ||
            request.DeliveryTypeCode == DeliveryType.ConditionalRecurring)
        {
            frequencyCode = request.FrequencyCode;
            frequencyInterval = request.FrequencyInterval;
            weeklyDayOfWeek = request.WeeklyDayOfWeek;
            monthlyDay = request.MonthlyDay;
            endDate = request.EndDate;
        }

        TopUpCampaign campaign = TopUpCampaign.Create(
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
            weeklyDayOfWeek: weeklyDayOfWeek,
            monthlyDay: monthlyDay,
            deliveryTypeCode: request.DeliveryTypeCode,
            maxTotalAmount: request.MaxTotalAmount,
            currentUserId: currentUser.UserAccountId ?? 0,
            nowUtc: clock.UtcNow.UtcDateTime);

        await campaigns.AddAsync(campaign, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        await audit.RecordSchoolActionAsync(
            new SchoolAuditContext(
                AuditActionCodes.TopUpCampaignCreated,
                "TopUpCampaign",
                campaign.Id,
                campaign.OrganizationId,
                new SchoolAuditDetails(
                    "Top-up campaign created",
                    EntityDisplayName: campaign.CampaignName)),
            cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<long>.Success(campaign.Id);
    }
}
