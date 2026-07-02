using FluentValidation;
using Moe.Application.Abstractions.Clock;
using Moe.Modules.EducationAccountTopUp.Contracts.TopUps.DTOs;
using Moe.Modules.EducationAccountTopUp.Contracts.TopUps.Enums;
using Moe.Modules.EducationAccountTopUp.Domain.TopUps;

namespace Moe.Modules.EducationAccountTopUp.Api.Admin;

public sealed class UpdateCampaignRequestValidator : AbstractValidator<UpdateCampaignRequest>
{
    public UpdateCampaignRequestValidator(IClock clock)
    {
        RuleFor(x => x.CampaignName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(1000);
        RuleFor(x => x.DefaultTopUpAmount).GreaterThan(0);
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(1000);

        RuleFor(x => x.ScheduleTypeCode)
            .IsEnumName(typeof(ScheduleTypeCode), caseSensitive: false);

        RuleFor(x => x.DeliveryTypeCode)
            .Must(type => DeliveryType.IsValid(type))
            .WithMessage("DeliveryTypeCode must be one of: INSTANT, FIXED_CONTRACT, CONDITIONAL_RECURRING.");

        RuleFor(x => x.MaxTotalAmount).GreaterThan(0);

        bool IsRecurringOrContract(UpdateCampaignRequest req) =>
            string.Equals(req.ScheduleTypeCode, ScheduleTypeCode.Recurring.ToString(), StringComparison.OrdinalIgnoreCase) ||
            req.DeliveryTypeCode == DeliveryType.FixedContract ||
            req.DeliveryTypeCode == DeliveryType.ConditionalRecurring;

        When(x => !string.Equals(x.ScheduleTypeCode, ScheduleTypeCode.Immediate.ToString(), StringComparison.OrdinalIgnoreCase) || IsRecurringOrContract(x), () =>
        {
            RuleFor(x => x.StartDate)
                .Must(startDate => startDate >= DateOnly.FromDateTime(clock.UtcNow.UtcDateTime))
                .WithMessage("StartDate must be today or in the future for scheduled or contract campaigns.");

            When(x => string.Equals(x.ScheduleTypeCode, ScheduleTypeCode.OneTimeScheduled.ToString(), StringComparison.OrdinalIgnoreCase) && !IsRecurringOrContract(x), () =>
            {
                RuleFor(x => x.EndDate).Null().WithMessage("EndDate is not applicable for OneTimeScheduled campaigns.");
            });

            When(IsRecurringOrContract, () =>
            {
                RuleFor(x => x.FrequencyCode)
                    .NotEmpty()
                    .IsEnumName(typeof(FrequencyCode), caseSensitive: false);
                RuleFor(x => x.FrequencyInterval).GreaterThan(0);
                RuleFor(x => x.EndDate)
                    .NotNull().WithMessage("EndDate is required for Recurring or Contract campaigns.")
                    .Must((request, endDate) => endDate >= request.StartDate)
                    .WithMessage("EndDate must be greater than or equal to StartDate.");
            });
        });
    }
}
