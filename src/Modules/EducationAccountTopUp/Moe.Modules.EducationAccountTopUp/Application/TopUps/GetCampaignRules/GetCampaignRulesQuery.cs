using Moe.Application.Abstractions.Messaging;

namespace Moe.Modules.EducationAccountTopUp.Application.TopUps.GetCampaignRules;

public sealed record GetCampaignRulesQuery(long TopUpCampaignId) : IQuery<IReadOnlyList<CampaignRuleGroupDto>>;

public sealed record CampaignRuleGroupDto(
    string Id,
    int DisplayOrder,
    IReadOnlyList<CampaignRuleDto> Criteria);

public sealed record CampaignRuleDto(
    string Id,
    int DisplayOrder,
    string CriterionCode,
    string OperatorCode,
    decimal? NumericValueFrom,
    decimal? NumericValueTo,
    string? TextValue);
