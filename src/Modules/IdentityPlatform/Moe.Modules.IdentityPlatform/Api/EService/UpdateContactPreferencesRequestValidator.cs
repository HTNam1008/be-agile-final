using FluentValidation;

namespace Moe.Modules.IdentityPlatform.Api.EService;

public sealed class UpdateContactPreferencesRequestValidator : AbstractValidator<UpdateContactPreferencesRequest>
{
    public UpdateContactPreferencesRequestValidator()
    {
        RuleFor(x => x.PreferredEmail)
            .MaximumLength(320)
            .EmailAddress()
            .When(x => !string.IsNullOrWhiteSpace(x.PreferredEmail));

        RuleFor(x => x.PreferredMobile)
            .MaximumLength(50)
            .Matches(@"^[0-9+()\-\s]*$")
            .When(x => !string.IsNullOrWhiteSpace(x.PreferredMobile))
            .WithMessage("Preferred mobile can contain only digits, spaces, plus, hyphen and parentheses.");

        RuleFor(x => x.PreferredAddress)
            .MaximumLength(1000)
            .When(x => !string.IsNullOrWhiteSpace(x.PreferredAddress));
    }
}
