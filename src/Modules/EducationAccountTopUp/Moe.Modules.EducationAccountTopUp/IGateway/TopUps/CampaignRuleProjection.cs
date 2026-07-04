namespace Moe.Modules.EducationAccountTopUp.IGateway.TopUps;

public sealed record CampaignRuleGroupProjection(
    long GroupId,
    int DisplayOrder,
    IReadOnlyList<CampaignRuleProjection> Criteria);

public sealed record CampaignRuleProjection(
    long Id,
    long GroupId,
    int DisplayOrder,
    string CriterionCode,
    string OperatorCode,
    decimal? NumericValueFrom,
    decimal? NumericValueTo,
    string? TextValue);
