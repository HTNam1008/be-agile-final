namespace Moe.Modules.EducationAccountTopUp.IGateway.TopUps;

internal sealed record TopUpAccountSearchCriteria(
    string? Search,
    decimal? BalanceFrom,
    decimal? BalanceTo,
    string? AccountStatusCode);
