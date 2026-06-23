using Microsoft.AspNetCore.Mvc;
using Moe.SharedKernel.Results;

namespace Moe.Infrastructure.Shared.Api;

public static class ControllerResultExtensions
{
    public static IActionResult ToApiResponse<T>(
        this Result<T> result,
        ControllerBase controller,
        int failureStatusCode = ApiResponseCodes.Conflict,
        string successMessage = "Success")
    {
        return result.IsFailure
            ? ApiResponseFactory.Failure(result.Error, failureStatusCode, controller.HttpContext.TraceIdentifier)
            : ApiResponseFactory.Ok(result.Value, controller.HttpContext.TraceIdentifier, successMessage);
    }

    public static IActionResult ToCreatedApiResponse<T>(
        this Result<T> result,
        ControllerBase controller,
        int failureStatusCode = ApiResponseCodes.Conflict,
        string successMessage = "Created")
    {
        return result.IsFailure
            ? ApiResponseFactory.Failure(result.Error, failureStatusCode, controller.HttpContext.TraceIdentifier)
            : ApiResponseFactory.Created(result.Value, controller.HttpContext.TraceIdentifier, successMessage);
    }
}
