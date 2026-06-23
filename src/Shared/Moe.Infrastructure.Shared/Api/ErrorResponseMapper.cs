using Microsoft.AspNetCore.Http;
using Moe.SharedKernel.Results;

namespace Moe.Infrastructure.Shared.Api;

public static class ErrorResponseMapper
{
    public static ApiResponse<object> ToApiResponse(
        Error error,
        int statusCode,
        string? traceId = null)
    {
        return ApiResponse<object>.Fail(
            error.Message,
            [error.Code],
            statusCode,
            traceId);
    }

    public static IResult ToApiResult(
        Error error,
        int statusCode,
        string? traceId = null)
    {
        return Results.Json(
            ToApiResponse(error, statusCode, traceId),
            statusCode: statusCode);
    }
}
