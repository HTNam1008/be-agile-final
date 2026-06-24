using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Moe.Application.Abstractions.Messaging;
using Moe.Infrastructure.Shared.Api;
using Moe.Infrastructure.Shared.Security;
using Moe.Modules.Mfa.Application.ChangePin;
using Moe.Modules.Mfa.Application.GetMfaStatus;
using Moe.Modules.Mfa.Application.SetupPin;
using Moe.Modules.Mfa.Application.StartChallenge;
using Moe.Modules.Mfa.Application.VerifyPin;

namespace Moe.Modules.Mfa.Api;

[ApiController]
[ApiVersion(1.0)]
[Route("api/mfa/v{version:apiVersion}")]
[EnableCors("PortalCors")]
public sealed class MfaController(
    ICommandDispatcher commands,
    IQueryDispatcher queries) : ControllerBase
{
    [HttpPost("challenge")]
    [Authorize(AuthenticationSchemes = $"{AuthenticationSchemes.AdminEntra},{AuthenticationSchemes.EServiceSingpass}")]
    public async Task<IActionResult> StartChallenge(
        CancellationToken cancellationToken)
    {
        var result = await commands.Send(
            new StartMfaChallengeCommand(),
            cancellationToken);

        return result.ToApiResponse(this, ApiResponseCodes.BadRequest);
    }

    [HttpGet("status")]
    [Authorize(AuthenticationSchemes = $"{AuthenticationSchemes.AdminEntra},{AuthenticationSchemes.EServiceSingpass}")]
    public async Task<IActionResult> Status(CancellationToken cancellationToken)
    {
        var result = await queries.Send(new GetMfaStatusQuery(), cancellationToken);
        return result.ToApiResponse(this, ApiResponseCodes.Unauthorized);
    }

    [HttpPost("setup")]
    [Authorize(AuthenticationSchemes = $"{AuthenticationSchemes.AdminEntra},{AuthenticationSchemes.EServiceSingpass}")]
    public async Task<IActionResult> Setup([FromBody] SetupMfaPinRequest request, CancellationToken cancellationToken)
    {
        var result = await commands.Send(new SetupMfaPinCommand(request.Pin), cancellationToken);
        return result.ToApiResponse(this, ApiResponseCodes.BadRequest); 
    }

    [HttpPost("verify")]
    [Authorize(AuthenticationSchemes = $"{AuthenticationSchemes.AdminEntra},{AuthenticationSchemes.EServiceSingpass}")]
    public async Task<IActionResult> Verify([FromBody] VerifyMfaPinRequest request, CancellationToken cancellationToken)
    {
        var result = await commands.Send(new VerifyMfaPinCommand(request.ChallengeId, request.Pin), cancellationToken);
        return result.ToApiResponse(this, ApiResponseCodes.Unauthorized);
    }

    [HttpPost("pin/update")]
    [Authorize(AuthenticationSchemes = $"{AuthenticationSchemes.AdminEntra},{AuthenticationSchemes.EServiceSingpass}")]
    public async Task<IActionResult> ChangePin([FromBody] ChangeMfaPinRequest request, CancellationToken cancellationToken)
    {
        var result = await commands.Send(new ChangeMfaPinCommand(request.OldPin, request.NewPin), cancellationToken);
        return result.ToApiResponse(this, ApiResponseCodes.Unauthorized);
    }
}
