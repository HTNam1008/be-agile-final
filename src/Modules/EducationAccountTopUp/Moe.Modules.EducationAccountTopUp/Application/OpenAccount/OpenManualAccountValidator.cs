using FluentValidation;

namespace Moe.Modules.EducationAccountTopUp.Application.OpenAccount;

public sealed class OpenManualAccountValidator : AbstractValidator<OpenManualAccountCommand>
{
    public OpenManualAccountValidator()
    {
        RuleFor(x => x.PersonId).GreaterThan(0);
        RuleFor(x => x.ReasonCode).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Remarks).NotEmpty().MaximumLength(1000);
    }
}
