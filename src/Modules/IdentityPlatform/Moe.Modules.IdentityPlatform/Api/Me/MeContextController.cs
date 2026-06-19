using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Moe.Application.Abstractions.Messaging;
using Moe.Infrastructure.Shared.Api;
using Moe.Infrastructure.Shared.Security;
using Moe.Modules.IdentityPlatform.Application.Authentication.GetCurrentIdentity;

namespace Moe.Modules.IdentityPlatform.Api.Me;

[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/me")]
[Authorize(AuthenticationSchemes = $"{AuthenticationSchemes.AdminEntra},{AuthenticationSchemes.EServiceSingpass}")]
[EnableCors("PortalCors")]
public sealed class MeContextController(IQueryDispatcher queries) : ControllerBase
{
    [HttpGet("context")]
    public async Task<IActionResult> Context(CancellationToken cancellationToken)
    {
        var result = await queries.Send(new GetCurrentIdentityQuery(), cancellationToken);
        return result.ToApiResponse(this, ApiResponseCodes.Forbidden);
    }
}
