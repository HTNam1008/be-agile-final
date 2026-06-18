using FluentValidation;

namespace Moe.Modules.IdentityPlatform.Api.EService;

public sealed class UpdateMyStudentContactRequestValidator : AbstractValidator<UpdateMyStudentContactRequest>
{
    public UpdateMyStudentContactRequestValidator()
    {
        RuleFor(x => x.ContactEmail)
            .MaximumLength(320)
            .EmailAddress()
            .When(x => !string.IsNullOrWhiteSpace(x.ContactEmail));

        RuleFor(x => x.ContactMobile)
            .MaximumLength(30)
            .Matches(@"^[0-9+()\-\s]*$")
            .When(x => !string.IsNullOrWhiteSpace(x.ContactMobile))
            .WithMessage("Contact mobile can contain only digits, spaces, plus, hyphen and parentheses.");
    }
}
