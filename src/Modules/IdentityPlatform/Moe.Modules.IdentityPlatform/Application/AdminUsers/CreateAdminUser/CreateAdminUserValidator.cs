using FluentValidation;

namespace Moe.Modules.IdentityPlatform.Application.AdminUsers.CreateAdminUser;

public sealed class CreateAdminUserValidator : AbstractValidator<CreateAdminUserCommand>
{
    public CreateAdminUserValidator()
    {
        RuleFor(x => x.Email)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(320);

        RuleFor(x => x.DisplayName)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.MailNickname)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .MaximumLength(64)
            .Matches("^[A-Za-z0-9._-]+$")
            .WithMessage("Mail nickname can contain only letters, numbers, dots, underscores, and hyphens.");

        RuleFor(x => x.TemporaryPassword)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .MinimumLength(12)
            .MaximumLength(128)
            .Matches("[A-Z]").WithMessage("Temporary password must contain an uppercase letter.")
            .Matches("[a-z]").WithMessage("Temporary password must contain a lowercase letter.")
            .Matches("[0-9]").WithMessage("Temporary password must contain a number.")
            .Matches("[^A-Za-z0-9]").WithMessage("Temporary password must contain a symbol.");

        RuleFor(x => x.InitialOrganizationUnitId).GreaterThan(0);
    }
}
