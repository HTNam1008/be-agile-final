using FluentValidation;

namespace Moe.Modules.EducationAccountTopUp.Application.TopUps.DeleteCampaign;

public sealed class DeleteCampaignCommandValidator : AbstractValidator<DeleteCampaignCommand>
{
    public DeleteCampaignCommandValidator()
    {
        RuleFor(x => x.TopUpCampaignId).GreaterThan(0);
    }
}
