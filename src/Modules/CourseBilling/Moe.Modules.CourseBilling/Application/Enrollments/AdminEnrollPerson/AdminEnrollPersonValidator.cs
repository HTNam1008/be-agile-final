using FluentValidation;
using Moe.Modules.CourseBilling.Contracts.AdminEnrollments;

namespace Moe.Modules.CourseBilling.Application.Enrollments.AdminEnrollPerson;

public sealed class AdminEnrollPersonRequestValidator : AbstractValidator<AdminEnrollPersonRequest>
{
    public AdminEnrollPersonRequestValidator()
    {
        RuleFor(x => x.PersonId).GreaterThan(0);
    }
}
