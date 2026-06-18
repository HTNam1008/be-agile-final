using FluentValidation;
using Moe.Modules.EducationAccountTopUp.Contracts.TopUps.Enums;

namespace Moe.Modules.EducationAccountTopUp.Application.TopUps.ChangeCampaignStatus;

public sealed class ChangeCampaignStatusCommandValidator : AbstractValidator<ChangeCampaignStatusCommand>
{
    public ChangeCampaignStatusCommandValidator()
    {
        RuleFor(x => x.TopUpCampaignId).GreaterThan(0);
        RuleFor(x => x.NewStatusCode)
            .NotEmpty()
            .IsEnumName(typeof(TopUpCampaignStatusCode), caseSensitive: false);
    }
}
