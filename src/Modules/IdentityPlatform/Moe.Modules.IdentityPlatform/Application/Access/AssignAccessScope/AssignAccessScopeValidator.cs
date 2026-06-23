using FluentValidation;

namespace Moe.Modules.IdentityPlatform.Application.Access.AssignAccessScope;

public sealed class AssignAccessScopeValidator : AbstractValidator<AssignAccessScopeCommand>
{
    public AssignAccessScopeValidator()
    {
        RuleFor(x => x.UserAccountId).GreaterThan(0);
        RuleFor(x => x.OrganizationUnitId).GreaterThan(0);
        RuleFor(x => x.RoleCode)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .MaximumLength(50)
            .Matches("^[A-Z0-9_]+$")
            .WithMessage("Role code must use uppercase letters, numbers, and underscores.");
    }
}
