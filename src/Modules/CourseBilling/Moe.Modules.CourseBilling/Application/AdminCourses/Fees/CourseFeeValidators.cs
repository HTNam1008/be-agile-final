using FluentValidation;
using Moe.Modules.CourseBilling.Contracts.AdminCourses;

namespace Moe.Modules.CourseBilling.Application.AdminCourses.Fees;

internal sealed class CreateCourseFeeRequestValidator : AbstractValidator<CreateCourseFeeRequest>
{
    public CreateCourseFeeRequestValidator()
    {
        RuleFor(x => x.FeeComponentId).GreaterThan(0);
        RuleFor(x => x.FeeValue).GreaterThanOrEqualTo(0);
        RuleFor(x => x.SequenceNumber).GreaterThanOrEqualTo(0);
    }
}

internal sealed class UpdateCourseFeeRequestValidator : AbstractValidator<UpdateCourseFeeRequest>
{
    public UpdateCourseFeeRequestValidator()
    {
        RuleFor(x => x.FeeValue).GreaterThanOrEqualTo(0);
        RuleFor(x => x.SequenceNumber).GreaterThanOrEqualTo(0);
    }
}
