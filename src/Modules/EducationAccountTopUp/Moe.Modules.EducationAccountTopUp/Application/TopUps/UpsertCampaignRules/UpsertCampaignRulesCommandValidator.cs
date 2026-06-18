using FluentValidation;
using Moe.Modules.EducationAccountTopUp.Contracts.TopUps.Enums;

namespace Moe.Modules.EducationAccountTopUp.Application.TopUps.UpsertCampaignRules;

public sealed class UpsertCampaignRulesCommandValidator : AbstractValidator<UpsertCampaignRulesCommand>
{
    public UpsertCampaignRulesCommandValidator()
    {
        RuleFor(x => x.TopUpCampaignId).GreaterThan(0);
        RuleFor(x => x.Rules).NotNull()
            .Must(rules => rules.Select(r => r.CriterionCode.ToUpperInvariant()).Distinct().Count() == rules.Count)
            .WithMessage("Duplicate criteria are not allowed. Each criterion (e.g. Age, AccountBalance) can only be used once per campaign.");

        RuleForEach(x => x.Rules).ChildRules(rule =>
        {
            rule.RuleFor(x => x.CriterionCode)
                .NotEmpty()
                .IsEnumName(typeof(TopUpCriterionCode), caseSensitive: false);

            rule.RuleFor(x => x.OperatorCode)
                .NotEmpty()
                .IsEnumName(typeof(OperatorCode), caseSensitive: false);

            rule.When(x => 
                string.Equals(x.CriterionCode, TopUpCriterionCode.Age.ToString(), StringComparison.OrdinalIgnoreCase) ||
                string.Equals(x.CriterionCode, TopUpCriterionCode.AccountBalance.ToString(), StringComparison.OrdinalIgnoreCase), () =>
            {
                rule.RuleFor(x => x.OperatorCode).Must(op => 
                    string.Equals(op, OperatorCode.GreaterThan.ToString(), StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(op, OperatorCode.LessThan.ToString(), StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(op, OperatorCode.GreaterThanOrEqual.ToString(), StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(op, OperatorCode.LessThanOrEqual.ToString(), StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(op, OperatorCode.Between.ToString(), StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(op, OperatorCode.Equals.ToString(), StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(op, OperatorCode.NotEquals.ToString(), StringComparison.OrdinalIgnoreCase))
                .WithMessage("Numeric criteria must use valid numeric operators.");

                rule.RuleFor(x => x.NumericValueFrom).NotNull();

                rule.When(x => string.Equals(x.OperatorCode, OperatorCode.Between.ToString(), StringComparison.OrdinalIgnoreCase), () =>
                {
                    rule.RuleFor(x => x.NumericValueTo)
                        .NotNull()
                        .GreaterThanOrEqualTo(x => x.NumericValueFrom);
                });
            });

            rule.When(x => 
                string.Equals(x.CriterionCode, TopUpCriterionCode.SchoolingStatus.ToString(), StringComparison.OrdinalIgnoreCase) ||
                string.Equals(x.CriterionCode, TopUpCriterionCode.Level.ToString(), StringComparison.OrdinalIgnoreCase) ||
                string.Equals(x.CriterionCode, TopUpCriterionCode.Class.ToString(), StringComparison.OrdinalIgnoreCase), () =>
            {
                rule.RuleFor(x => x.OperatorCode).Must(op => 
                    string.Equals(op, OperatorCode.Equals.ToString(), StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(op, OperatorCode.NotEquals.ToString(), StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(op, OperatorCode.In.ToString(), StringComparison.OrdinalIgnoreCase))
                .WithMessage("Text criteria must use valid text operators (Equals, NotEquals, In).");

                rule.RuleFor(x => x.TextValue).NotEmpty();

                rule.When(x => string.Equals(x.OperatorCode, OperatorCode.In.ToString(), StringComparison.OrdinalIgnoreCase), () =>
                {
                    rule.RuleFor(x => x.TextValue).Must(val => 
                    {
                        if (string.IsNullOrWhiteSpace(val)) return false;
                        try {
                            var list = System.Text.Json.JsonSerializer.Deserialize<System.Collections.Generic.List<string>>(val);
                            return list != null && list.Count > 0;
                        } catch { return false; }
                    }).WithMessage("TextValue must be a valid, non-empty JSON array of strings for the 'In' operator.");
                });
            });
        });
    }
}
