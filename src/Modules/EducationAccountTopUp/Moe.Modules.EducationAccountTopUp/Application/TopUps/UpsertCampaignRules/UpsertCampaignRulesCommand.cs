using Moe.Application.Abstractions.Messaging;

namespace Moe.Modules.EducationAccountTopUp.Application.TopUps.UpsertCampaignRules;

public sealed record UpsertCampaignRuleDto(
    string CriterionCode,
    string OperatorCode,
    decimal? NumericValueFrom,
    decimal? NumericValueTo,
    string? TextValue);

public sealed record UpsertCampaignRulesCommand(
    long TopUpCampaignId,
    List<UpsertCampaignRuleDto> Rules) : ICommand;
