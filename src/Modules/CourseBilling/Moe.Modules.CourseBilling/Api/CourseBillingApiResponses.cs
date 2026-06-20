using Microsoft.AspNetCore.Mvc;
using Moe.Infrastructure.Shared.Api;
using Moe.SharedKernel.Results;

namespace Moe.Modules.CourseBilling.Api;

internal static class CourseBillingApiResponses
{
    public static IActionResult ToCourseBillingResponse<T>(
        this ControllerBase controller,
        Result<T> result,
        bool created = false)
    {
        if (result.IsSuccess)
        {
            return created
                ? ApiResponseFactory.Created(result.Value, controller.HttpContext.TraceIdentifier)
                : ApiResponseFactory.Ok(result.Value, controller.HttpContext.TraceIdentifier);
        }

        return ApiResponseFactory.Failure(
            result.Error,
            GetFailureStatusCode(result.Error),
            controller.HttpContext.TraceIdentifier);
    }

    private static int GetFailureStatusCode(Error error)
        => error.Code switch
        {
            "COURSE.ADMIN_REQUIRED" => ApiResponseCodes.Forbidden,
            "COURSE.ORGANIZATION_FORBIDDEN" => ApiResponseCodes.Forbidden,
            "COURSE.NOT_FOUND" => ApiResponseCodes.NotFound,
            "COURSE.MATERIAL_NOT_FOUND" => ApiResponseCodes.NotFound,
            "COURSE.FEE_NOT_FOUND" => ApiResponseCodes.NotFound,
            "COURSE.ENROLLMENT_NOT_FOUND" => ApiResponseCodes.NotFound,
            "COURSE.FEE_COMPONENT_DUPLICATE_CODE" => ApiResponseCodes.Conflict,
            _ => ApiResponseCodes.BadRequest
        };
}
