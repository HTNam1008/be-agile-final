using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Moe.Infrastructure.Shared.Api;
using Moe.Infrastructure.Shared.Security;
using Moe.Modules.CourseBilling.Application.AdminFeeComponents;
using Moe.SharedKernel.Results;

namespace Moe.Modules.CourseBilling.Api.Admin;

[ApiController]
[ApiVersion(1.0)]
[Route("api/admin/v{version:apiVersion}/fee-components")]
[Authorize(Policy = AuthorizationPolicies.AdminPortal)]
//[Authorize(Policy = AuthorizationPolicies.ManageCourses)]
[EnableCors("AdminCors")]
public sealed class AdminFeeComponentsController(IAdminFeeComponentService feeComponents) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] FeeComponentQueryRequest request, CancellationToken cancellationToken)
        => ToResponse(await feeComponents.ListAsync(request, cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateFeeComponentRequest request, CancellationToken cancellationToken)
        => ToResponse(await feeComponents.CreateAsync(request, cancellationToken), created: true);

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
            "COURSE.FEE_COMPONENT_DUPLICATE_CODE" => ApiResponseCodes.Conflict,
            _ => ApiResponseCodes.BadRequest
        };
}
