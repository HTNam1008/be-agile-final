using Microsoft.AspNetCore.Mvc;
using Moe.SharedKernel.Results;

namespace Moe.Infrastructure.Shared.Api;

public static class ApiResponseFactory
{
    public static OkObjectResult Ok<T>(
        T? data,
        string? traceId = null,
        string message = "Success")
    {
        return new OkObjectResult(ApiResponse<T>.Ok(data, message, ApiResponseCodes.Ok, traceId));
    }

    public static ObjectResult Created<T>(
        T? data,
        string? traceId = null,
        string message = "Created")
    {
        return new ObjectResult(ApiResponse<T>.Created(data, message, traceId))
        {
            StatusCode = ApiResponseCodes.Created
        };
    }

    public static ObjectResult Accepted<T>(
        T? data,
        string? traceId = null,
        string message = "Accepted")
    {
        return new ObjectResult(ApiResponse<T>.Accepted(data, message, traceId))
        {
            StatusCode = ApiResponseCodes.Accepted
        };
    }

    public static ObjectResult NoContent(
        string? traceId = null,
        string message = "No content")
    {
        return new ObjectResult(ApiResponse<object>.NoContent(message, traceId))
        {
            StatusCode = ApiResponseCodes.NoContent
        };
    }

    public static ObjectResult Failure(
        Error error,
        int statusCode,
        string? traceId = null)
    {
        return new ObjectResult(ApiResponse<object>.Fail(error.Message, [error.Code], statusCode, traceId))
        {
            StatusCode = statusCode
        };
    }
}
