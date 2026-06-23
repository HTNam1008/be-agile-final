using FluentValidation;

namespace Moe.Modules.EducationAccountTopUp.Application.TopUps.ExecuteRun;

public sealed class ExecuteTopUpRunCommandValidator : AbstractValidator<ExecuteTopUpRunCommand>
{
    public ExecuteTopUpRunCommandValidator()
    {
        RuleFor(x => x.TopUpCampaignId).GreaterThan(0);
    }
}
