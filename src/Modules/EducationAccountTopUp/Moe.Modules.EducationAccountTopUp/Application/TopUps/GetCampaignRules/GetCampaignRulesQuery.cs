using Moe.Application.Abstractions.Messaging;

namespace Moe.Modules.EducationAccountTopUp.Application.TopUps.GetCampaignRules;

public sealed record GetCampaignRulesQuery(long TopUpCampaignId) : IQuery<IReadOnlyList<CampaignRuleDto>>;

public sealed record CampaignRuleDto(
    string Id,
    string CriterionCode,
    string OperatorCode,
    decimal? NumericValueFrom,
    decimal? NumericValueTo,
    string? TextValue);
