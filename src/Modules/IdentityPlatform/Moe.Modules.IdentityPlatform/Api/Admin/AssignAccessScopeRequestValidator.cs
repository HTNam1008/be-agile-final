using FluentValidation;

namespace Moe.Modules.IdentityPlatform.Api.Admin;

public sealed class AssignAccessScopeRequestValidator : AbstractValidator<AssignAccessScopeRequest>
{
    public AssignAccessScopeRequestValidator()
    {
        RuleFor(x => x.OrganizationUnitId).GreaterThan(0);
        RuleFor(x => x.RoleCode)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .MaximumLength(50)
            .Matches("^[A-Z0-9_]+$")
            .WithMessage("Role code must use uppercase letters, numbers, and underscores.");
    }
}
