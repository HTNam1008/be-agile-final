namespace Moe.Modules.EducationAccountTopUp.Application.TopUps.SearchAccounts;

public sealed record SearchTopUpAccountsResponse(
    IReadOnlyList<TopUpAccountSearchItem> Items,
    int Page,
    int PageSize,
    long TotalCount);
