using FluentValidation;
using Moe.Modules.CourseBilling.Contracts.AdminCourses;

namespace Moe.Modules.CourseBilling.Application.AdminCourses.Courses;

internal sealed class CreateCourseRequestValidator : AbstractValidator<CreateCourseRequest>
{
    public CreateCourseRequestValidator()
    {
        RuleFor(x => x.CourseCode).NotEmpty().MaximumLength(50);
        RuleFor(x => x.CourseName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.BeforeStartRefundPercentage).InclusiveBetween(0m, 100m);
        RuleFor(x => x.AfterStartRefundPercentage).InclusiveBetween(0m, 100m);
    }
}

internal sealed class UpdateCourseRequestValidator : AbstractValidator<UpdateCourseRequest>
{
    public UpdateCourseRequestValidator()
    {
        RuleFor(x => x.CourseCode).NotEmpty().MaximumLength(50);
        RuleFor(x => x.CourseName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.BeforeStartRefundPercentage).InclusiveBetween(0m, 100m);
        RuleFor(x => x.AfterStartRefundPercentage).InclusiveBetween(0m, 100m);
    }
}
