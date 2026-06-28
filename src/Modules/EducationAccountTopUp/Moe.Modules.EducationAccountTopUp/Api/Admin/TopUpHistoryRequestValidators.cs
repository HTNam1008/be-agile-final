using FluentValidation;
using Moe.Modules.EducationAccountTopUp.Contracts.TopUps.Enums;
using Moe.Modules.EducationAccountTopUp.Domain.TopUps;

namespace Moe.Modules.EducationAccountTopUp.Api.Admin;

public sealed class CampaignHistoryRequestValidator : AbstractValidator<CampaignHistoryRequest>
{
    public CampaignHistoryRequestValidator()
    {
        RuleFor(x => x.CampaignId).GreaterThan(0).When(x => x.CampaignId.HasValue);
        RuleFor(x => x.CampaignSearch).MaximumLength(100);
        RuleFor(x => x.OrganizationId).GreaterThan(0).When(x => x.OrganizationId.HasValue);
        RuleFor(x => x.ActorId).GreaterThan(0).When(x => x.ActorId.HasValue);
        RuleFor(x => x.Page).InclusiveBetween(1, TopUpHistoryValidationRules.MaxPageNumber);
        RuleFor(x => x.PageSize).InclusiveBetween(1, TopUpHistoryValidationRules.MaxPageSize);
        RuleFor(x => x)
            .Must(x => TopUpHistoryValidationRules.HasValidDateRange(
                x.DateFromUtc,
                x.DateToUtc))
            .WithMessage("DateFromUtc must be earlier than DateToUtc.");

        RuleFor(x => x.Status)
            .Must(TopUpCampaignStatusCodes.IsValid!)
            .When(x => !string.IsNullOrWhiteSpace(x.Status))
            .WithMessage("Status must be a valid campaign status.");
    }
}

public sealed class RunHistoryRequestValidator : AbstractValidator<RunHistoryRequest>
{
    public RunHistoryRequestValidator()
    {
        RuleFor(x => x.CampaignId).GreaterThan(0).When(x => x.CampaignId.HasValue);
        RuleFor(x => x.CampaignSearch).MaximumLength(100);
        RuleFor(x => x.OrganizationId).GreaterThan(0).When(x => x.OrganizationId.HasValue);
        RuleFor(x => x.ActorId).GreaterThan(0).When(x => x.ActorId.HasValue);
        RuleFor(x => x.Page).InclusiveBetween(1, TopUpHistoryValidationRules.MaxPageNumber);
        RuleFor(x => x.PageSize).InclusiveBetween(1, TopUpHistoryValidationRules.MaxPageSize);
        RuleFor(x => x)
            .Must(x => TopUpHistoryValidationRules.HasValidDateRange(
                x.DateFromUtc,
                x.DateToUtc))
            .WithMessage("DateFromUtc must be earlier than DateToUtc.");

        RuleFor(x => x.TriggerType)
            .Must(TopUpHistoryValidationRules.BeValidEnumValue<TopUpTriggerTypeCode>)
            .When(x => !string.IsNullOrWhiteSpace(x.TriggerType))
            .WithMessage("TriggerType must be MANUAL or SCHEDULED.");

        RuleFor(x => x.Status)
            .Must(TopUpHistoryValidationRules.BeValidEnumValue<TopUpRunStatusCode>)
            .When(x => !string.IsNullOrWhiteSpace(x.Status))
            .WithMessage("Status must be a valid run status.");

        RuleFor(x => x.StudentOrAccountSearch).MaximumLength(100);
    }
}

public sealed class CampaignTransactionHistoryRequestValidator : AbstractValidator<CampaignTransactionHistoryRequest>
{
    public CampaignTransactionHistoryRequestValidator()
    {
        RuleFor(x => x.OrganizationId).GreaterThan(0).When(x => x.OrganizationId.HasValue);
        RuleFor(x => x.Page).InclusiveBetween(1, TopUpHistoryValidationRules.MaxPageNumber);
        RuleFor(x => x.PageSize).InclusiveBetween(1, TopUpHistoryValidationRules.MaxPageSize);
        RuleFor(x => x)
            .Must(x => TopUpHistoryValidationRules.HasValidDateRange(
                x.DateFromUtc,
                x.DateToUtc))
            .WithMessage("DateFromUtc must be earlier than DateToUtc.");
    }
}

public sealed class AccountTransactionHistoryRequestValidator : AbstractValidator<AccountTransactionHistoryRequest>
{
    public AccountTransactionHistoryRequestValidator()
    {
        RuleFor(x => x.Page).InclusiveBetween(1, TopUpHistoryValidationRules.MaxPageNumber);
        RuleFor(x => x.PageSize).InclusiveBetween(1, TopUpHistoryValidationRules.MaxPageSize);
        RuleFor(x => x)
            .Must(x => TopUpHistoryValidationRules.HasValidDateRange(
                x.DateFromUtc,
                x.DateToUtc))
            .WithMessage("DateFromUtc must be earlier than DateToUtc.");
    }
}

internal static class TopUpHistoryValidationRules
{
    internal const int MaxPageSize = 100;
    internal const int MaxPageNumber = 1_000_000;

    internal static bool HasValidDateRange(DateTime? from, DateTime? to)
        => !from.HasValue || !to.HasValue || from.Value < to.Value;

    internal static bool BeValidEnumValue<TEnum>(string? value)
        where TEnum : struct, Enum
        => string.IsNullOrWhiteSpace(value)
            || Enum.TryParse(value.Trim(), ignoreCase: true, out TEnum _);
}
