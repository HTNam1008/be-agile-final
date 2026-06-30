using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Moe.Infrastructure.Shared.Api;
using Moe.Infrastructure.Shared.Security;
using Moe.Modules.IdentityPlatform.Application.Audit;
using Moe.SharedKernel.Results;

namespace Moe.Modules.IdentityPlatform.Api.Admin;

[ApiController]
[ApiVersion(1.0)]
[Route("api/admin/v{version:apiVersion}/school-audit-logs")]
[Authorize(Policy = AuthorizationPolicies.AdminPortal)]
[EnableCors("AdminCors")]
public sealed class SchoolAuditLogsController(ISchoolAuditLogReader reader) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] long? organizationId,
        [FromQuery] string? actionCode,
        [FromQuery] string? entityTypeCode,
        [FromQuery] long? actorId,
        [FromQuery] DateTime? dateFromUtc,
        [FromQuery] DateTime? dateToUtc,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        SchoolAuditLogReadResult result = await reader.ReadAsync(
            new SchoolAuditLogQuery(
                organizationId,
                actionCode,
                entityTypeCode,
                actorId,
                dateFromUtc,
                dateToUtc,
                page,
                pageSize),
            cancellationToken);

        return result.Status switch
        {
            SchoolAuditLogReadStatus.Success => ApiResponseFactory.Ok(result.Page!, HttpContext.TraceIdentifier),
            SchoolAuditLogReadStatus.OrganizationRequired => Failure(
                "AUDIT.ORGANIZATION_REQUIRED",
                "An organizationId is required when the school admin has multiple school scopes.",
                ApiResponseCodes.BadRequest),
            SchoolAuditLogReadStatus.OrganizationOutsideScope => Failure(
                "AUDIT.ORGANIZATION_OUTSIDE_SCOPE",
                "The requested organization is outside the current school admin's scope.",
                ApiResponseCodes.Forbidden),
            _ => Failure(
                "AUDIT.SCHOOL_ADMIN_REQUIRED",
                "School audit logs are available to school administrators only.",
                ApiResponseCodes.Forbidden)
        };
    }

    private IActionResult Failure(string code, string message, int statusCode)
        => ApiResponseFactory.Failure(new Error(code, message), statusCode, HttpContext.TraceIdentifier);
}
