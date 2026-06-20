using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Moe.Application.Abstractions.Messaging;
using Moe.Infrastructure.Shared.Api;
using Moe.Infrastructure.Shared.Security;
using Moe.Modules.CourseBilling.Application.Dashboard.GetAdminDashboard;

namespace Moe.Modules.CourseBilling.Api.Admin;

[ApiController]
[ApiVersion(1.0)]
[Route("api/admin/v{version:apiVersion}/dashboard")]
[Authorize(Policy = AuthorizationPolicies.AdminPortal)]
[EnableCors("AdminCors")]
public sealed class AdminDashboardController(IQueryDispatcher queries) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery] int? year,
        [FromQuery] long? organizationId,
        CancellationToken cancellationToken)
    {
        var result = await queries.Send(new GetAdminDashboardQuery(year, organizationId), cancellationToken);
        return result.ToApiResponse(this, ApiResponseCodes.BadRequest);
    }
}
