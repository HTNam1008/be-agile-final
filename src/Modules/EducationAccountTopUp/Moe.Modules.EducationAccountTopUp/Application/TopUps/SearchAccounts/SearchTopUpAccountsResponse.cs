using System.Text.Json.Serialization;

namespace Moe.Modules.EducationAccountTopUp.Application.TopUps.SearchAccounts;

public sealed record SearchTopUpAccountsResponse(
    IReadOnlyList<TopUpAccountSearchItem> Items,
    [property: JsonPropertyName("pageNumber")] int Page,
    int PageSize,
    long TotalCount);
