using FluentValidation;

namespace Moe.Modules.CourseBilling.Api.EService;

public sealed class SelfJoinCourseRequestValidator : AbstractValidator<SelfJoinCourseRequest>
{
    public SelfJoinCourseRequestValidator()
    {
        RuleFor(x => x.CourseId).GreaterThan(0);
    }
}
