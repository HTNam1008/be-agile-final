using System.ComponentModel.DataAnnotations;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Moe.Application.Abstractions.Messaging;
using Moe.Infrastructure.Shared.Api;
using Moe.Infrastructure.Shared.Security;
using Moe.Modules.CourseBilling.Application.Dashboard.RoleDashboards;

namespace Moe.Modules.CourseBilling.Api.Admin;

[ApiController]
[ApiVersion(1.0)]
[Route("api/admin/v{version:apiVersion}/dashboard")]
[Authorize(Policy = AuthorizationPolicies.AdminPortal)]
[EnableCors("AdminCors")]
public sealed class AdminRoleDashboardController(IQueryDispatcher queries) : ControllerBase
{
    [HttpGet("hq")]
    public async Task<IActionResult> GetHq(
        [FromQuery, Range(2000, 2100)] int? year,
        CancellationToken cancellationToken)
    {
        var result = await queries.Send(new GetHqDashboardQuery(year), cancellationToken);
        return result.ToApiResponse(this, ApiResponseCodes.Forbidden);
    }

    [HttpGet("school")]
    public async Task<IActionResult> GetSchool(
        [FromQuery, Range(2000, 2100)] int? year,
        CancellationToken cancellationToken)
    {
        var result = await queries.Send(new GetSchoolDashboardQuery(year), cancellationToken);
        return result.ToApiResponse(this, ApiResponseCodes.Forbidden);
    }
}
