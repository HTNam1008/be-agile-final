using FluentValidation;
using Moe.Modules.EducationAccountTopUp.Domain.EducationAccounts;

namespace Moe.Modules.EducationAccountTopUp.Application.CloseAccount;

public sealed class CloseManualAccountValidator : AbstractValidator<CloseManualAccountCommand>
{
    public CloseManualAccountValidator()
    {
        RuleFor(x => x.PersonId).GreaterThan(0);
        RuleFor(x => x.ReasonCode)
            .NotEmpty()
            .MaximumLength(50)
            .Must(reason => EducationAccountClosingReasonCodes.All.Contains(reason))
            .WithMessage("ReasonCode is not supported.");
        RuleFor(x => x.Remarks).MaximumLength(1000);
    }
}
