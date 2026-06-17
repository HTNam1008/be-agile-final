namespace Moe.Modules.EducationAccountTopUp.Api.Admin;

public sealed class SearchTopUpAccountsRequest
{
    public string? Search { get; init; }
    public long? OrganizationId { get; init; }
    public string? SchoolingStatusCode { get; init; }
    public string? LevelCode { get; init; }
    public string? ClassCode { get; init; }
    public string? AccountStatusCode { get; init; }
    public int? AgeFrom { get; init; }
    public int? AgeTo { get; init; }
    public decimal? BalanceFrom { get; init; }
    public decimal? BalanceTo { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 25;
}
