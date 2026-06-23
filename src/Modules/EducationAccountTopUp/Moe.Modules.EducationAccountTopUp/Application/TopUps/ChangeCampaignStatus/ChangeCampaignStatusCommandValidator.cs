using FluentValidation;
using Moe.Modules.EducationAccountTopUp.Domain.TopUps;

namespace Moe.Modules.EducationAccountTopUp.Application.TopUps.ChangeCampaignStatus;

public sealed class ChangeCampaignStatusCommandValidator : AbstractValidator<ChangeCampaignStatusCommand>
{
    public ChangeCampaignStatusCommandValidator()
    {
        RuleFor(x => x.TopUpCampaignId).GreaterThan(0);
        RuleFor(x => x.NewStatusCode)
            .NotEmpty()
            .Must(TopUpCampaignStatusCodes.IsValid)
            .WithMessage("NewStatusCode must be a valid campaign status.");
    }
}
