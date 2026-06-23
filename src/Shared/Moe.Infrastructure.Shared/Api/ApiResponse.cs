namespace Moe.Infrastructure.Shared.Api;

public sealed class ApiResponse<T>
{
    public bool Success { get; init; }
    public int Code { get; init; }
    public string Message { get; init; } = string.Empty;
    public T? Data { get; init; }
    public IReadOnlyCollection<string>? Errors { get; init; }
    public string? TraceId { get; init; }
    public DateTime TimestampUtc { get; init; } = DateTime.UtcNow;

    public static ApiResponse<T> Ok(
        T? data,
        string message = "Success",
        int code = ApiResponseCodes.Ok,
        string? traceId = null)
    {
        return new ApiResponse<T>
        {
            Success = true,
            Code = code,
            Message = message,
            Data = data,
            TraceId = traceId,
            TimestampUtc = DateTime.UtcNow
        };
    }

    public static ApiResponse<T> Created(
        T? data,
        string message = "Created",
        string? traceId = null)
    {
        return Ok(data, message, ApiResponseCodes.Created, traceId);
    }

    public static ApiResponse<T> Accepted(
        T? data,
        string message = "Accepted",
        string? traceId = null)
    {
        return Ok(data, message, ApiResponseCodes.Accepted, traceId);
    }

    public static ApiResponse<T> NoContent(
        string message = "No content",
        string? traceId = null)
    {
        return Ok(default, message, ApiResponseCodes.NoContent, traceId);
    }

    public static ApiResponse<T> Fail(
        string message,
        IReadOnlyCollection<string>? errors = null,
        int code = ApiResponseCodes.BadRequest,
        string? traceId = null)
    {
        return new ApiResponse<T>
        {
            Success = false,
            Code = code,
            Message = message,
            Errors = errors,
            TraceId = traceId,
            TimestampUtc = DateTime.UtcNow
        };
    }
}
