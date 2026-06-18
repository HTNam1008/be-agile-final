using Moe.Application.Abstractions.Messaging;

namespace Moe.Modules.EducationAccountTopUp.Application.TopUps.SearchAccounts;

public sealed record SearchTopUpAccountsQuery(
    string? Search,
    long? OrganizationId,
    string? SchoolingStatusCode,
    string? LevelCode,
    string? ClassCode,
    string? AccountStatusCode,
    int? AgeFrom,
    int? AgeTo,
    decimal? BalanceFrom,
    decimal? BalanceTo,
    int Page,
    int PageSize) : IQuery<SearchTopUpAccountsResponse>;
