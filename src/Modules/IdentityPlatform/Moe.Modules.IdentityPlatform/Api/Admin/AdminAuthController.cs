using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Moe.Application.Abstractions.Messaging;
using Moe.Infrastructure.Shared.Api;
using Moe.Infrastructure.Shared.Security;
using Moe.Modules.IdentityPlatform.Application.Authentication.GetAdminAuthFlow;
using Moe.Modules.IdentityPlatform.Application.Authentication.GetCurrentIdentity;
using Moe.Modules.IdentityPlatform.IGateway.Authentication;

namespace Moe.Modules.IdentityPlatform.Api.Admin;

[ApiController]
[ApiVersion(1.0)]
[Route("api/admin/v{version:apiVersion}/auth")]
[EnableCors("AdminCors")]
public sealed class AdminAuthController(IQueryDispatcher queries) : ControllerBase
{
    [HttpGet("flow")]
    [AllowAnonymous]
    public async Task<IActionResult> GetFlow(CancellationToken cancellationToken)
    {
        var result = await queries.Send(new GetAdminAuthFlowQuery(), cancellationToken);
        return ApiResponseFactory.Ok(result.Value, HttpContext.TraceIdentifier);
    }

    [HttpGet("me")]
    [Authorize(Policy = AuthorizationPolicies.AdminPortal)]
    public async Task<IActionResult> Me(CancellationToken cancellationToken)
    {
        var result = await queries.Send(new GetCurrentIdentityQuery(), cancellationToken);
        return result.ToApiResponse(this, ApiResponseCodes.Forbidden);
    }
}
