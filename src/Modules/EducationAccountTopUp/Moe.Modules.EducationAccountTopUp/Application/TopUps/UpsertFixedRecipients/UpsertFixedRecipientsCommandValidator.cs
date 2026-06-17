using FluentValidation;

namespace Moe.Modules.EducationAccountTopUp.Application.TopUps.UpsertFixedRecipients;

public sealed class UpsertFixedRecipientsCommandValidator : AbstractValidator<UpsertFixedRecipientsCommand>
{
    public UpsertFixedRecipientsCommandValidator()
    {
        RuleFor(x => x.TopUpCampaignId).GreaterThan(0);
        RuleFor(x => x.Recipients).NotNull();

        RuleForEach(x => x.Recipients).ChildRules(recipient =>
        {
            recipient.RuleFor(x => x.EducationAccountId).GreaterThan(0);
            
            // AmountOverride is nullable, but if provided, must be > 0.
            recipient.When(x => x.AmountOverride.HasValue, () =>
            {
                recipient.RuleFor(x => x.AmountOverride!.Value)
                    .GreaterThan(0)
                    .WithMessage("AmountOverride must be greater than 0 if provided.");
            });
        });
    }
}
