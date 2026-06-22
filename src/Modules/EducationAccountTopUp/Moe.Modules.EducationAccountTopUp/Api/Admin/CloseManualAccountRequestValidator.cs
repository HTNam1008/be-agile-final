using FluentValidation;
using Moe.Modules.EducationAccountTopUp.Domain.EducationAccounts;

namespace Moe.Modules.EducationAccountTopUp.Api.Admin;

public sealed class CloseManualAccountRequestValidator : AbstractValidator<CloseManualAccountRequest>
{
    public CloseManualAccountRequestValidator()
    {
        RuleFor(x => x.ReasonCode)
            .NotEmpty()
            .MaximumLength(50)
            .Must(reason => EducationAccountClosingReasonCodes.All.Contains(reason))
            .WithMessage("ReasonCode is not supported.");
        RuleFor(x => x.Remarks).MaximumLength(1000);
    }
}
