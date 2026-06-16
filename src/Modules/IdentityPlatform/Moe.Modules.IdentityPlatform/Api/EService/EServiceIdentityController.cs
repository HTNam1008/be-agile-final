using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Moe.Application.Abstractions.Messaging;
using Moe.Infrastructure.Shared.Api;
using Moe.Infrastructure.Shared.Security;
using Moe.Modules.IdentityPlatform.Application.Authentication.GetCurrentIdentity;

namespace Moe.Modules.IdentityPlatform.Api.EService;

[ApiController]
[ApiVersion(1.0)]
[Route("api/eservice/v{version:apiVersion}")]
[Authorize(Policy = AuthorizationPolicies.EServicePortal)]
[EnableCors("EServiceCors")]
public sealed class EServiceIdentityController(IQueryDispatcher queries) : ControllerBase
{
    [HttpGet("me")]
    public async Task<IActionResult> Me(CancellationToken cancellationToken)
    {
        var result = await queries.Send(new GetCurrentIdentityQuery(), cancellationToken);
        return result.ToApiResponse(this, ApiResponseCodes.Forbidden);
    }
}
