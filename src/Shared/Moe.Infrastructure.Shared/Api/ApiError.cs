namespace Moe.Infrastructure.Shared.Api;

public sealed record ApiError(
    string Code,
    string Message,
    string? TraceId = null);
