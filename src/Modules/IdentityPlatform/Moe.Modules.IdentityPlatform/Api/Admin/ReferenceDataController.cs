using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Moe.Application.Abstractions.Messaging;
using Moe.Infrastructure.Shared.Api;
using Moe.Infrastructure.Shared.Security;
using Moe.Modules.IdentityPlatform.Application.ReferenceData;

namespace Moe.Modules.IdentityPlatform.Api.Admin;

[ApiController]
[ApiVersion(1.0)]
[Route("api/admin/v{version:apiVersion}/reference-data")]
[Authorize(Policy = AuthorizationPolicies.AdminPortal)]
[EnableCors("AdminCors")]
public sealed class ReferenceDataController(IQueryDispatcher queries) : ControllerBase
{
    [HttpGet("student-management")]
    public async Task<IActionResult> GetStudentManagement(CancellationToken cancellationToken)
    {
        var result = await queries.Send(new GetStudentManagementReferenceDataQuery(), cancellationToken);
        return result.IsFailure
            ? ApiResponseFactory.Failure(result.Error, ApiResponseCodes.Conflict, HttpContext.TraceIdentifier)
            : ApiResponseFactory.Ok(result.Value, HttpContext.TraceIdentifier);
    }
}
