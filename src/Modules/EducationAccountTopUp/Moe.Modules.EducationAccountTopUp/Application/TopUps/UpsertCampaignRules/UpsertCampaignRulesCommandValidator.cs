using FluentValidation;
using Moe.Modules.EducationAccountTopUp.Contracts.TopUps.Enums;

namespace Moe.Modules.EducationAccountTopUp.Application.TopUps.UpsertCampaignRules;

public sealed class UpsertCampaignRulesCommandValidator : AbstractValidator<UpsertCampaignRulesCommand>
{
    public UpsertCampaignRulesCommandValidator()
    {
        RuleFor(x => x.TopUpCampaignId).GreaterThan(0);
        RuleFor(x => x.Groups).NotNull();
        RuleFor(x => x.Groups).Must(groups => groups.Count <= 10).WithMessage("Max 10 rule groups.");

        RuleForEach(x => x.Groups).ChildRules(group =>
        {
            group.RuleFor(x => x.Criteria).NotNull().NotEmpty();
            group.RuleFor(x => x.Criteria).Must(criteria => criteria.Count <= 10).WithMessage("Max 10 criteria per group.");
            group.RuleFor(x => x.Criteria)
                .Must(criteria => criteria.Select(r => r.CriterionCode.ToUpperInvariant()).Distinct().Count() == criteria.Count)
                .WithMessage("Duplicate criteria are not allowed within a group.");

            group.RuleForEach(x => x.Criteria).SetValidator(new UpsertCampaignRuleDtoValidator());
        });
    }
}

internal sealed class UpsertCampaignRuleDtoValidator : AbstractValidator<UpsertCampaignRuleDto>
{
    public UpsertCampaignRuleDtoValidator()
    {
        RuleFor(x => x.CriterionCode)
            .NotEmpty()
            .IsEnumName(typeof(TopUpCriterionCode), caseSensitive: false);

        RuleFor(x => x.OperatorCode)
            .NotEmpty()
            .IsEnumName(typeof(OperatorCode), caseSensitive: false);

        When(x =>
            string.Equals(x.CriterionCode, TopUpCriterionCode.Age.ToString(), StringComparison.OrdinalIgnoreCase) ||
            string.Equals(x.CriterionCode, TopUpCriterionCode.AccountBalance.ToString(), StringComparison.OrdinalIgnoreCase), () =>
        {
            RuleFor(x => x.OperatorCode).Must(op =>
                string.Equals(op, OperatorCode.GreaterThan.ToString(), StringComparison.OrdinalIgnoreCase) ||
                string.Equals(op, OperatorCode.LessThan.ToString(), StringComparison.OrdinalIgnoreCase) ||
                string.Equals(op, OperatorCode.GreaterThanOrEqual.ToString(), StringComparison.OrdinalIgnoreCase) ||
                string.Equals(op, OperatorCode.LessThanOrEqual.ToString(), StringComparison.OrdinalIgnoreCase) ||
                string.Equals(op, OperatorCode.Between.ToString(), StringComparison.OrdinalIgnoreCase) ||
                string.Equals(op, OperatorCode.Equals.ToString(), StringComparison.OrdinalIgnoreCase) ||
                string.Equals(op, OperatorCode.NotEquals.ToString(), StringComparison.OrdinalIgnoreCase))
            .WithMessage("Numeric criteria must use valid numeric operators.");

            RuleFor(x => x.NumericValueFrom).NotNull();
            RuleFor(x => x.TextValue).Empty();

            When(x => string.Equals(x.OperatorCode, OperatorCode.Between.ToString(), StringComparison.OrdinalIgnoreCase), () =>
            {
                RuleFor(x => x.NumericValueTo)
                    .NotNull()
                    .GreaterThanOrEqualTo(x => x.NumericValueFrom);
            });
        });

        When(x =>
            string.Equals(x.CriterionCode, TopUpCriterionCode.SchoolingStatus.ToString(), StringComparison.OrdinalIgnoreCase) ||
            string.Equals(x.CriterionCode, TopUpCriterionCode.Level.ToString(), StringComparison.OrdinalIgnoreCase) ||
            string.Equals(x.CriterionCode, TopUpCriterionCode.Class.ToString(), StringComparison.OrdinalIgnoreCase), () =>
        {
            RuleFor(x => x.OperatorCode).Must(op =>
                string.Equals(op, OperatorCode.Equals.ToString(), StringComparison.OrdinalIgnoreCase) ||
                string.Equals(op, OperatorCode.NotEquals.ToString(), StringComparison.OrdinalIgnoreCase) ||
                string.Equals(op, OperatorCode.In.ToString(), StringComparison.OrdinalIgnoreCase))
            .WithMessage("Text criteria must use valid text operators (Equals, NotEquals, In).");

            RuleFor(x => x.TextValue).NotEmpty();
            RuleFor(x => x.NumericValueFrom).Null();
            RuleFor(x => x.NumericValueTo).Null();

            When(x => string.Equals(x.OperatorCode, OperatorCode.In.ToString(), StringComparison.OrdinalIgnoreCase), () =>
            {
                RuleFor(x => x.TextValue).Must(val =>
                {
                    if (string.IsNullOrWhiteSpace(val)) return false;
                    try
                    {
                        var list = System.Text.Json.JsonSerializer.Deserialize<List<string>>(val);
                        return list != null && list.Count > 0;
                    }
                    catch { return false; }
                }).WithMessage("TextValue must be a valid, non-empty JSON array of strings for the 'In' operator.");
            });
        });

        When(x => string.Equals(x.CriterionCode, TopUpCriterionCode.HasEducationAccount.ToString(), StringComparison.OrdinalIgnoreCase), () =>
        {
            RuleFor(x => x.OperatorCode)
                .Must(op => string.Equals(op, OperatorCode.Equals.ToString(), StringComparison.OrdinalIgnoreCase))
                .WithMessage("Edu Account must use Equals.");

            RuleFor(x => x.TextValue)
                .Must(value =>
                    string.Equals(value, "YES", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(value, "NO", StringComparison.OrdinalIgnoreCase))
                .WithMessage("Edu Account must be YES or NO.");

            RuleFor(x => x.NumericValueFrom).Null();
            RuleFor(x => x.NumericValueTo).Null();
        });
    }
}
