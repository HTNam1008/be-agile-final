using FluentValidation;
using Moe.Modules.EducationAccountTopUp.Contracts.TopUps.Enums;

namespace Moe.Modules.EducationAccountTopUp.Application.TopUps.UpdateCampaign;

public sealed class UpdateCampaignCommandValidator : AbstractValidator<UpdateCampaignCommand>
{
    public UpdateCampaignCommandValidator()
    {
        RuleFor(x => x.TopUpCampaignId).GreaterThan(0);
        RuleFor(x => x.Request.CampaignName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Request.Description).MaximumLength(1000);
        RuleFor(x => x.Request.DefaultTopUpAmount).GreaterThan(0);
        RuleFor(x => x.Request.Reason).NotEmpty().MaximumLength(1000);
        
        RuleFor(x => x.Request.ScheduleTypeCode)
            .IsEnumName(typeof(ScheduleTypeCode), caseSensitive: false);

        When(x => !string.Equals(x.Request.ScheduleTypeCode, ScheduleTypeCode.Immediate.ToString(), StringComparison.OrdinalIgnoreCase), () =>
        {
            RuleFor(x => x.Request.StartDate)
                .Must(startDate => startDate >= DateOnly.FromDateTime(DateTime.UtcNow))
                .WithMessage("StartDate must be today or in the future for scheduled campaigns.");

            When(x => string.Equals(x.Request.ScheduleTypeCode, ScheduleTypeCode.OneTimeScheduled.ToString(), StringComparison.OrdinalIgnoreCase), () =>
            {
                RuleFor(x => x.Request.EndDate).Null().WithMessage("EndDate is not applicable for OneTimeScheduled campaigns.");
            });

            When(x => string.Equals(x.Request.ScheduleTypeCode, ScheduleTypeCode.Recurring.ToString(), StringComparison.OrdinalIgnoreCase), () =>
            {
                RuleFor(x => x.Request.FrequencyCode)
                    .NotEmpty()
                    .IsEnumName(typeof(FrequencyCode), caseSensitive: false);
                RuleFor(x => x.Request.FrequencyInterval).GreaterThan(0);
                RuleFor(x => x.Request.EndDate)
                    .NotNull().WithMessage("EndDate is required for Recurring campaigns.")
                    .Must((request, endDate) => endDate >= request.Request.StartDate)
                    .WithMessage("EndDate must be greater than or equal to StartDate.");
            });
        });
    }
}
