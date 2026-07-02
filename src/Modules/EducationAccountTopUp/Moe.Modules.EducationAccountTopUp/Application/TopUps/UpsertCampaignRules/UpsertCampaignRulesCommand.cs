using Moe.Application.Abstractions.Messaging;

namespace Moe.Modules.EducationAccountTopUp.Application.TopUps.UpsertCampaignRules;

public sealed record UpsertCampaignRuleDto(
    string CriterionCode,
    string OperatorCode,
    decimal? NumericValueFrom,
    decimal? NumericValueTo,
    string? TextValue);

public sealed record UpsertRuleGroupDto(List<UpsertCampaignRuleDto> Criteria);

public sealed record UpsertCampaignRulesCommand(
    long TopUpCampaignId,
    List<UpsertRuleGroupDto> Groups) : ICommand;
