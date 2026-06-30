using FluentValidation;
using Moe.Modules.CourseBilling.Contracts.AdminFeeComponents;
using Moe.Modules.CourseBilling.Domain.Courses;

namespace Moe.Modules.CourseBilling.Application.AdminFeeComponents;

internal sealed class CreateFeeComponentRequestValidator : AbstractValidator<CreateFeeComponentRequest>
{
    public CreateFeeComponentRequestValidator()
    {
        RuleFor(x => x.ComponentCode).NotEmpty().MaximumLength(50);
        RuleFor(x => x.ComponentName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.ComponentTypeCode)
            .NotEmpty()
            .MaximumLength(30)
            .Must(value => FeeComponentTypeCodes.All.Contains(value.Trim().ToUpperInvariant()))
            .WithMessage("Fee component type must be BASE, ADDON or TAX.");
        RuleFor(x => x.CalculationTypeCode).NotEmpty().MaximumLength(30);
        RuleFor(x => x.DefaultValue).GreaterThanOrEqualTo(0m);
    }
}

internal sealed class UpdateFeeComponentRequestValidator : AbstractValidator<UpdateFeeComponentRequest>
{
    public UpdateFeeComponentRequestValidator()
    {
        RuleFor(x => x.ComponentCode).NotEmpty().MaximumLength(50);
        RuleFor(x => x.ComponentName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.ComponentTypeCode)
            .NotEmpty()
            .MaximumLength(30)
            .Must(value => FeeComponentTypeCodes.All.Contains(value.Trim().ToUpperInvariant()))
            .WithMessage("Fee component type must be BASE, ADDON or TAX.");
        RuleFor(x => x.CalculationTypeCode).NotEmpty().MaximumLength(30);
        RuleFor(x => x.DefaultValue).GreaterThanOrEqualTo(0m);
    }
}
