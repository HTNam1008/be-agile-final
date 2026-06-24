using FluentValidation;

namespace Moe.Modules.IdentityPlatform.Api.Admin;

public sealed class UpdateAdminAccountDetailsRequestValidator : AbstractValidator<UpdateAdminAccountDetailsRequest>
{
    public UpdateAdminAccountDetailsRequestValidator()
    {
        RuleFor(x => x.ClassCode).MaximumLength(30).When(x => x.ClassCode is not null);
        RuleFor(x => x.ResidentialAddress).MaximumLength(1000).When(x => x.ResidentialAddress is not null);
        RuleFor(x => x.Email)
            .MaximumLength(320)
            .EmailAddress()
            .When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.ContactNumber)
            .MaximumLength(50)
            .Matches(@"^[0-9+()\-\s]*$")
            .When(x => !string.IsNullOrWhiteSpace(x.ContactNumber))
            .WithMessage("Contact number may only contain digits, spaces, +, -, and parentheses.");
    }
}
