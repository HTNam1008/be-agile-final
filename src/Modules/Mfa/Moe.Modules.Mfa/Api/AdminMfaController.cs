using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Moe.Application.Abstractions.Messaging;
using Moe.Infrastructure.Shared.Api;
using Moe.Infrastructure.Shared.Security;
using Moe.Modules.Mfa.Application.ResetPin;

namespace Moe.Modules.Mfa.Api;

[ApiController]
[ApiVersion(1.0)]
[Route("api/admin/v{version:apiVersion}/mfa")]
[Authorize(Policy = AuthorizationPolicies.AdminPortal)]
[EnableCors("AdminCors")]
public sealed class AdminMfaController(ICommandDispatcher commands) : ControllerBase
{
    [HttpPost("pin/reset")]
    public async Task<IActionResult> ResetPin(
        [FromBody] ResetMfaPinRequest request,
        CancellationToken cancellationToken)
    {
        var result = await commands.Send(
            new ResetMfaPinCommand(request.LoginAccountId, request.Reason),
            cancellationToken);

        return result.ToApiResponse(this, ApiResponseCodes.BadRequest);
    }
}
