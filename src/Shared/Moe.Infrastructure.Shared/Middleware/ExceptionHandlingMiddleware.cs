using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moe.Infrastructure.Shared.Api;
using Moe.Infrastructure.Shared.Exceptions;
using Moe.Infrastructure.Shared.Security;

namespace Moe.Infrastructure.Shared.Middleware;

public sealed class ExceptionHandlingMiddleware(
    RequestDelegate next,
    IHostEnvironment environment,
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
            LogHandledFailure(context, exception, StatusCodes.Status400BadRequest, "VALIDATION_FAILED");
            await WriteAsync(
                context,
                StatusCodes.Status400BadRequest,
                "VALIDATION_FAILED",
                ApiErrorMessages.ValidationFailed,
                exception.Errors.Select(error => error.ErrorMessage).Distinct().ToArray());
        }
        catch (KeyNotFoundException exception)
        {
            LogHandledFailure(context, exception, StatusCodes.Status404NotFound, "NOT_FOUND");
            await WriteAsync(
                context,
                StatusCodes.Status404NotFound,
                "NOT_FOUND",
                ApiErrorMessages.NotFound);
        }
        catch (UnauthorizedAccessException exception)
        {
            bool authenticationRequired = exception.Message.Contains("AUTHENTICATION_REQUIRED", StringComparison.OrdinalIgnoreCase);
            int statusCode = authenticationRequired ? StatusCodes.Status401Unauthorized : StatusCodes.Status403Forbidden;
            LogHandledFailure(context, exception, statusCode, authenticationRequired ? "UNAUTHORIZED" : "FORBIDDEN");
            await WriteAsync(
                context,
                statusCode,
                authenticationRequired ? "UNAUTHORIZED" : "FORBIDDEN",
                authenticationRequired ? ApiErrorMessages.Unauthorized : ApiErrorMessages.Forbidden);
        }
        catch (ApiException exception)
        {
            LogHandledFailure(context, exception, exception.StatusCode, exception.Code);
            await WriteAsync(
                context,
                exception.StatusCode,
                exception.Code,
                ApiErrorMessages.ForStatusCode(
                    exception.StatusCode,
                    IsSafeClientMessage(exception.Message) ? exception.Message : null));
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Unhandled request failure. TraceId={TraceId}; Method={Method}; Path={Path}; Route={Route}; UserAccountId={UserAccountId}; PersonId={PersonId}; Portal={Portal}; Roles={Roles}",
                context.TraceIdentifier,
                context.Request.Method,
                context.Request.Path,
                context.GetEndpoint()?.DisplayName ?? string.Empty,
                context.User.FindFirst(ClaimNames.UserAccountId)?.Value ?? string.Empty,
                context.User.FindFirst(ClaimNames.PersonId)?.Value ?? string.Empty,
                context.User.FindFirst(ClaimNames.Portal)?.Value ?? string.Empty,
                string.Join(",", context.User.FindAll(ClaimNames.Role).Select(claim => claim.Value).Distinct()));
            await WriteAsync(
                context,
                StatusCodes.Status500InternalServerError,
                "UNEXPECTED_ERROR",
                environment.IsDevelopment()
                    ? $"{exception.GetType().Name}: {exception.Message}"
                    : ApiErrorMessages.Unexpected,
                environment.IsDevelopment()
                    ? [exception.GetType().FullName ?? exception.GetType().Name, exception.Message]
            : null);
        }
    }

    private void LogHandledFailure(HttpContext context, Exception exception, int statusCode, string errorCode)
    {
        logger.LogWarning(
            exception,
            "Handled request failure {ErrorCode} => {StatusCode}. TraceId={TraceId}; Method={Method}; Path={Path}; Route={Route}; UserAccountId={UserAccountId}; PersonId={PersonId}; Portal={Portal}; Roles={Roles}",
            errorCode,
            statusCode,
            context.TraceIdentifier,
            context.Request.Method,
            context.Request.Path,
            context.GetEndpoint()?.DisplayName ?? string.Empty,
            context.User.FindFirst(ClaimNames.UserAccountId)?.Value ?? string.Empty,
            context.User.FindFirst(ClaimNames.PersonId)?.Value ?? string.Empty,
            context.User.FindFirst(ClaimNames.Portal)?.Value ?? string.Empty,
            string.Join(",", context.User.FindAll(ClaimNames.Role).Select(claim => claim.Value).Distinct()));
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

    private static bool IsSafeClientMessage(string message)
        => !string.IsNullOrWhiteSpace(message)
           && message is not "Bad Request"
           && message is not "Unauthorized"
           && message is not "Forbidden"
           && message is not "Not Found"
           && message is not "Internal Server Error"
           && !message.All(character => char.IsUpper(character) || char.IsDigit(character) || character is '_' or '.' or ':' or '-')
           && !message.Contains("Exception", StringComparison.OrdinalIgnoreCase)
           && !message.Contains("Sql", StringComparison.OrdinalIgnoreCase)
           && !message.Contains("Trace", StringComparison.OrdinalIgnoreCase)
           && !message.Contains("Stack", StringComparison.OrdinalIgnoreCase)
           && !message.Contains("AUTHENTICATION_REQUIRED", StringComparison.OrdinalIgnoreCase);
}
