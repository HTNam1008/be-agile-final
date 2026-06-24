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
            "AUTH.ORGANIZATION_OUTSIDE_SCOPE" => ApiResponseCodes.Forbidden,
            "COURSE.SYSTEM_FEE_COMPONENT_FORBIDDEN" => ApiResponseCodes.Forbidden,
            "COURSE.SYSTEM_COURSE_FEE_FORBIDDEN" => ApiResponseCodes.Forbidden,
            "COURSE.NOT_FOUND" => ApiResponseCodes.NotFound,
            "COURSE.MATERIAL_NOT_FOUND" => ApiResponseCodes.NotFound,
            "COURSE.FEE_NOT_FOUND" => ApiResponseCodes.NotFound,
            "COURSE.GST_COMPONENT_NOT_CONFIGURED" => ApiResponseCodes.NotFound,
            "COURSE.ENROLLMENT_NOT_FOUND" => ApiResponseCodes.NotFound,
            "COURSE.FEE_COMPONENT_DUPLICATE_CODE" => ApiResponseCodes.Conflict,
            "COURSE.FEE_COMPONENT_IN_USE" => ApiResponseCodes.Conflict,
            "COURSE.DISABLED" => ApiResponseCodes.Conflict,
            "COURSE.NOT_PUBLISHED" => ApiResponseCodes.Conflict,
            "COURSE.ENROLLMENT_WINDOW_CLOSED" => ApiResponseCodes.Conflict,
            "COURSE.CONTENT_NOT_OPEN" => ApiResponseCodes.Conflict,
            "COURSE.CONTENT_LOCKED" => ApiResponseCodes.Forbidden,
            "COURSE.PERSON_NOT_IN_ORGANIZATION" => ApiResponseCodes.Conflict,
            _ => ApiResponseCodes.BadRequest
        };
}
