namespace Moe.Modules.EducationAccountTopUp.IGateway.TopUps;

public sealed record CampaignRuleProjection(
    long Id,
    string CriterionCode,
    string OperatorCode,
    decimal? NumericValueFrom,
    decimal? NumericValueTo,
    string? TextValue);
