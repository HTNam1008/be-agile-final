using FluentValidation;
using Moe.Modules.EducationAccountTopUp.Contracts.TopUps.Enums;

namespace Moe.Modules.EducationAccountTopUp.Application.TopUps.UpsertCampaignRules;

public sealed class UpsertCampaignRulesCommandValidator : AbstractValidator<UpsertCampaignRulesCommand>
{
    public UpsertCampaignRulesCommandValidator()
    {
        RuleFor(x => x.TopUpCampaignId).GreaterThan(0);
        RuleFor(x => x.Rules).NotNull();

        RuleForEach(x => x.Rules).ChildRules(rule =>
        {
            rule.RuleFor(x => x.CriterionCode)
                .NotEmpty()
                .IsEnumName(typeof(TopUpCriterionCode), caseSensitive: false);

            rule.RuleFor(x => x.OperatorCode)
                .NotEmpty()
                .IsEnumName(typeof(OperatorCode), caseSensitive: false);

            // Conditional validation based on OperatorCode
            rule.When(x => string.Equals(x.OperatorCode, OperatorCode.Between.ToString(), StringComparison.OrdinalIgnoreCase), () =>
            {
                rule.RuleFor(x => x.NumericValueFrom).NotNull();
                rule.RuleFor(x => x.NumericValueTo)
                    .NotNull()
                    .GreaterThanOrEqualTo(x => x.NumericValueFrom);
            });

            rule.When(x => 
                string.Equals(x.OperatorCode, OperatorCode.Equals.ToString(), StringComparison.OrdinalIgnoreCase) ||
                string.Equals(x.OperatorCode, OperatorCode.NotEquals.ToString(), StringComparison.OrdinalIgnoreCase) ||
                string.Equals(x.OperatorCode, OperatorCode.In.ToString(), StringComparison.OrdinalIgnoreCase), () =>
            {
                rule.RuleFor(x => x.TextValue).NotEmpty().When(x => !x.NumericValueFrom.HasValue);
            });
        });
    }
}
