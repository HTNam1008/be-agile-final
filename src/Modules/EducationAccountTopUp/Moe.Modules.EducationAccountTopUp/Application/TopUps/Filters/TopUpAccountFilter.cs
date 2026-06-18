namespace Moe.Modules.EducationAccountTopUp.Application.TopUps.Filters;

public sealed record TopUpAccountFilter(
    string? Search,
    long? OrganizationId,
    string? SchoolingStatusCode,
    string? LevelCode,
    string? ClassCode,
    string? AccountStatusCode,
    int? AgeFrom,
    int? AgeTo,
    decimal? BalanceFrom,
    decimal? BalanceTo);
