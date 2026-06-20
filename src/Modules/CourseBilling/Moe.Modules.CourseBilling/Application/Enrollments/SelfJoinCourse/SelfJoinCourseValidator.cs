using FluentValidation;
using Moe.Modules.CourseBilling.Contracts.Enrollments;

namespace Moe.Modules.CourseBilling.Application.Enrollments.SelfJoinCourse;

public sealed class SelfJoinCourseRequestValidator : AbstractValidator<SelfJoinCourseRequest>
{
    public SelfJoinCourseRequestValidator()
    {
        RuleFor(x => x.CourseId).GreaterThan(0);
    }
}
