using FluentValidation;
using Moe.Modules.CourseBilling.Contracts.AdminCourses;

namespace Moe.Modules.CourseBilling.Application.AdminCourses.Materials;

internal sealed class CreateCourseMaterialRequestValidator : AbstractValidator<CreateCourseMaterialRequest>
{
    public CreateCourseMaterialRequestValidator()
    {
        RuleFor(x => x.MaterialTitle).NotEmpty().MaximumLength(200);
        RuleFor(x => x.MaterialTypeCode).NotEmpty().MaximumLength(40);
        RuleFor(x => x.DisplayOrder).GreaterThanOrEqualTo(0);
    }
}

internal sealed class UpdateCourseMaterialRequestValidator : AbstractValidator<UpdateCourseMaterialRequest>
{
    public UpdateCourseMaterialRequestValidator()
    {
        RuleFor(x => x.MaterialTitle).NotEmpty().MaximumLength(200);
        RuleFor(x => x.MaterialTypeCode).NotEmpty().MaximumLength(40);
        RuleFor(x => x.DisplayOrder).GreaterThanOrEqualTo(0);
    }
}
