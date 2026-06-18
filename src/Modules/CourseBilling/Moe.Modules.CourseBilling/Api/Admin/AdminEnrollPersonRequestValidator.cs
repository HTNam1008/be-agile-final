using FluentValidation;

namespace Moe.Modules.CourseBilling.Api.Admin;

public sealed class AdminEnrollPersonRequestValidator : AbstractValidator<AdminEnrollPersonRequest>
{
    public AdminEnrollPersonRequestValidator()
    {
        RuleFor(x => x.PersonId).GreaterThan(0);
    }
}
