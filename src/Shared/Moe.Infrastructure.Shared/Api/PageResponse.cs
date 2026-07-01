using System.Text.Json.Serialization;

namespace Moe.Infrastructure.Shared.Api;

public sealed record PageResponse<T>(
    IReadOnlyList<T> Items,
    [property: JsonPropertyName("pageNumber")] int Page,
    int PageSize,
    long TotalCount);
