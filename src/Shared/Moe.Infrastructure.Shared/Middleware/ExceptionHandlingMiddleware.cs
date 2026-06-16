using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moe.Infrastructure.Shared.Api;
using Moe.Infrastructure.Shared.Exceptions;

namespace Moe.Infrastructure.Shared.Middleware;

public sealed class ExceptionHandlingMiddleware(
    RequestDelegate next,
    ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (ValidationException exception)
        {
            await WriteAsync(
                context,
                StatusCodes.Status400BadRequest,
                "VALIDATION_FAILED",
                "Validation failed.",
                exception.Errors.Select(error => error.ErrorMessage).Distinct().ToArray());
        }
        catch (ApiException exception)
        {
            await WriteAsync(
                context,
                exception.StatusCode,
                exception.Code,
                exception.Message);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Unhandled request failure. TraceId={TraceId}", context.TraceIdentifier);
            await WriteAsync(
                context,
                StatusCodes.Status500InternalServerError,
                "UNEXPECTED_ERROR",
                "An unexpected error occurred.");
        }
    }

    private static async Task WriteAsync(
        HttpContext context,
        int statusCode,
        string errorCode,
        string message,
        IReadOnlyCollection<string>? errors = null)
    {
        if (context.Response.HasStarted)
        {
            return;
        }

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";

        ApiResponse<object> response = ApiResponse<object>.Fail(
            message,
            errors ?? [errorCode],
            statusCode,
            context.TraceIdentifier);

        await context.Response.WriteAsJsonAsync(response);
    }
}
