using FluentValidation;
using Moe.Modules.CourseBilling.Contracts.AdminEnrollments;

namespace Moe.Modules.CourseBilling.Application.Enrollments.AdminEnrollPerson;

public sealed class AdminEnrollPersonRequestValidator : AbstractValidator<AdminEnrollPersonRequest>
{
    public AdminEnrollPersonRequestValidator()
    {
        RuleFor(x => x.StudentNumber)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .MaximumLength(50);

        RuleFor(x => x.CoursePaymentPlanId).GreaterThan(0);
    }
}
