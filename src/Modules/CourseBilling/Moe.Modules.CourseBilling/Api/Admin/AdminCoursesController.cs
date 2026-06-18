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
[Route("api/admin/v{version:apiVersion}/courses")]
//[Authorize(Policy = AuthorizationPolicies.AdminPortal)]
//[Authorize(Policy = AuthorizationPolicies.ManageCourses)]
//[EnableCors("AdminCors")]
public sealed class AdminCoursesController(IAdminCourseService courses) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] CourseQueryRequest request, CancellationToken cancellationToken)
        => ToResponse(await courses.ListCoursesAsync(request, cancellationToken));

    [HttpGet("{courseId:long}")]
    public async Task<IActionResult> Get(long courseId, CancellationToken cancellationToken)
        => ToResponse(await courses.GetCourseAsync(courseId, cancellationToken));

    [HttpGet("{courseId:long}/preview")]
    public async Task<IActionResult> Preview(long courseId, CancellationToken cancellationToken)
        => ToResponse(await courses.PreviewCourseAsync(courseId, cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateCourseRequest request, CancellationToken cancellationToken)
        => ToResponse(await courses.CreateCourseAsync(request, cancellationToken), created: true);

    [HttpPut("{courseId:long}")]
    public async Task<IActionResult> Update(long courseId, [FromBody] UpdateCourseRequest request, CancellationToken cancellationToken)
        => ToResponse(await courses.UpdateCourseAsync(courseId, request, cancellationToken));

    [HttpPost("{courseId:long}/publish")]
    public async Task<IActionResult> Publish(long courseId, CancellationToken cancellationToken)
        => ToResponse(await courses.PublishCourseAsync(courseId, cancellationToken));

    [HttpPost("{courseId:long}/disable")]
    public async Task<IActionResult> Disable(long courseId, [FromBody] DisableCourseRequest request, CancellationToken cancellationToken)
        => ToResponse(await courses.DisableCourseAsync(courseId, request, cancellationToken));

    [HttpPost("{courseId:long}/enable")]
    public async Task<IActionResult> Enable(long courseId, CancellationToken cancellationToken)
        => ToResponse(await courses.EnableCourseAsync(courseId, cancellationToken));

    [HttpGet("{courseId:long}/fees")]
    public async Task<IActionResult> ListFees(long courseId, CancellationToken cancellationToken)
        => ToResponse(await courses.ListFeesAsync(courseId, cancellationToken));

    [HttpPost("{courseId:long}/fees")]
    public async Task<IActionResult> AddFee(long courseId, [FromBody] CreateCourseFeeRequest request, CancellationToken cancellationToken)
        => ToResponse(await courses.AddFeeAsync(courseId, request, cancellationToken), created: true);

    [HttpPut("{courseId:long}/fees/{courseFeeId:long}")]
    public async Task<IActionResult> UpdateFee(long courseId, long courseFeeId, [FromBody] UpdateCourseFeeRequest request, CancellationToken cancellationToken)
        => ToResponse(await courses.UpdateFeeAsync(courseId, courseFeeId, request, cancellationToken));

    [HttpGet("{courseId:long}/enrollments")]
    public async Task<IActionResult> ListEnrollments(long courseId, CancellationToken cancellationToken)
        => ToResponse(await courses.ListEnrollmentsAsync(courseId, cancellationToken));

        //[HttpPost("{courseId:long}/enrollments")]
        //public async Task<IActionResult> AssignStudents(long courseId, [FromBody] AssignStudentsToCourseRequest request, CancellationToken cancellationToken)
        //    => ToResponse(await courses.AssignStudentsAsync(courseId, request, cancellationToken), created: true);

    [HttpDelete("{courseId:long}/enrollments/{courseEnrollmentId:long}")]
    public async Task<IActionResult> RemoveEnrollment(long courseId, long courseEnrollmentId, CancellationToken cancellationToken)
        => ToResponse(await courses.RemoveEnrollmentAsync(courseId, courseEnrollmentId, cancellationToken));

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
            "COURSE.FEE_NOT_FOUND" => ApiResponseCodes.NotFound,
            "COURSE.ENROLLMENT_NOT_FOUND" => ApiResponseCodes.NotFound,
            _ => ApiResponseCodes.BadRequest
        };
}
