using FluentValidation;

namespace Moe.Modules.EducationAccountTopUp.Api.Admin;

public sealed class OpenManualAccountRequestValidator : AbstractValidator<OpenManualAccountRequest>
{
    public OpenManualAccountRequestValidator()
    {
        RuleFor(x => x.PersonId).GreaterThan(0);
        RuleFor(x => x.ReasonCode).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Remarks).NotEmpty().MaximumLength(1000);
    }
}
