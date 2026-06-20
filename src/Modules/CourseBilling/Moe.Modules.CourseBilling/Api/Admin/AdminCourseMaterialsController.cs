using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Moe.Infrastructure.Shared.Api;
using Moe.Infrastructure.Shared.Security;
using Moe.Modules.CourseBilling.Application.AdminCourses;
using Moe.SharedKernel.Results;

namespace Moe.Modules.CourseBilling.Api.Admin;

[ApiController]
[ApiVersion(1.0)]
[Route("api/admin/v{version:apiVersion}/courses/{courseId:long}/materials")]
[Authorize(Policy = AuthorizationPolicies.AdminPortal)]
//[Authorize(Policy = AuthorizationPolicies.ManageCourses)]
[EnableCors("AdminCors")]
public sealed class AdminCourseMaterialsController(IAdminCourseService courses) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(long courseId, CancellationToken cancellationToken)
        => ToResponse(await courses.ListMaterialsAsync(courseId, cancellationToken));

    [HttpPost]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Add(
        long courseId,
        [FromForm] CreateCourseMaterialRequest request,
        CancellationToken cancellationToken)
        => ToResponse(await courses.AddMaterialAsync(courseId, request, cancellationToken), created: true);

    [HttpPut("{courseMaterialId:long}")]
    public async Task<IActionResult> Update(
        long courseId,
        long courseMaterialId,
        [FromBody] UpdateCourseMaterialRequest request,
        CancellationToken cancellationToken)
        => ToResponse(await courses.UpdateMaterialAsync(courseId, courseMaterialId, request, cancellationToken));

    [HttpPost("{courseMaterialId:long}/replace-file")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> ReplaceFile(
        long courseId,
        long courseMaterialId,
        [FromForm] ReplaceCourseMaterialFileRequest request,
        CancellationToken cancellationToken)
        => ToResponse(await courses.ReplaceMaterialFileAsync(courseId, courseMaterialId, request, cancellationToken));

    [HttpDelete("{courseMaterialId:long}")]
    public async Task<IActionResult> Delete(long courseId, long courseMaterialId, CancellationToken cancellationToken)
        => ToResponse(await courses.DeleteMaterialAsync(courseId, courseMaterialId, cancellationToken));

    private IActionResult ToResponse<T>(Result<T> result, bool created = false)
    {
        if (result.IsSuccess)
        {
            return created
                ? ApiResponseFactory.Created(result.Value, HttpContext.TraceIdentifier)
                : ApiResponseFactory.Ok(result.Value, HttpContext.TraceIdentifier);
        }

        return ApiResponseFactory.Failure(result.Error, GetFailureStatusCode(result.Error), HttpContext.TraceIdentifier);
    }

    private static int GetFailureStatusCode(Error error)
        => error.Code switch
        {
            "COURSE.ADMIN_REQUIRED" => ApiResponseCodes.Forbidden,
            "COURSE.NOT_FOUND" => ApiResponseCodes.NotFound,
            "COURSE.MATERIAL_NOT_FOUND" => ApiResponseCodes.NotFound,
            _ => ApiResponseCodes.BadRequest
        };
}
