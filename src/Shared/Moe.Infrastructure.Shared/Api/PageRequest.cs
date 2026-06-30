namespace Moe.Infrastructure.Shared.Api;

public sealed record PageRequest(
    int Page = 1,
    int PageSize = 10);
