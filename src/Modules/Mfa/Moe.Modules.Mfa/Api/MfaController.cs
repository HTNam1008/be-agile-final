using System.Security.Claims;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Moe.Application.Abstractions.Messaging;
using Moe.Infrastructure.Shared.Api;
using Moe.Infrastructure.Shared.Security;
using Moe.Modules.Mfa.Application;
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
[Authorize(Policy = AuthorizationPolicies.MfaPortal)]
public sealed class MfaController(
    ICommandDispatcher commands,
    IQueryDispatcher queries,
    IMfaSessionProofService sessionProof) : ControllerBase
{
    private const string AuthDomainHeader = "X-MOE-Auth-Domain";

    [HttpPost("challenge")]
    public async Task<IActionResult> StartChallenge(
        CancellationToken cancellationToken)
    {
        if (!TrySelectAuthDomain())
        {
            return Unauthorized();
        }

        var result = await commands.Send(
            new StartMfaChallengeCommand(),
            cancellationToken);

        return result.ToApiResponse(this, ApiResponseCodes.BadRequest);
    }

    [HttpGet("status")]
    public async Task<IActionResult> Status(CancellationToken cancellationToken)
    {
        if (!TrySelectAuthDomain())
        {
            return Unauthorized();
        }

        var result = await queries.Send(new GetMfaStatusQuery(), cancellationToken);
        return result.ToApiResponse(this, ApiResponseCodes.Unauthorized);
    }

    [HttpPost("setup")]
    public async Task<IActionResult> Setup([FromBody] SetupMfaPinRequest request, CancellationToken cancellationToken)
    {
        if (!TrySelectAuthDomain())
        {
            return Unauthorized();
        }

        var result = await commands.Send(new SetupMfaPinCommand(request.Pin), cancellationToken);
        if (result.IsSuccess)
        {
            sessionProof.MarkCurrentSessionVerified();
        }
        return result.ToApiResponse(this, ApiResponseCodes.BadRequest); 
    }

    [HttpPost("verify")]
    public async Task<IActionResult> Verify([FromBody] VerifyMfaPinRequest request, CancellationToken cancellationToken)
    {
        if (!TrySelectAuthDomain())
        {
            return Unauthorized();
        }

        var result = await commands.Send(new VerifyMfaPinCommand(request.ChallengeId, request.Pin), cancellationToken);
        if (result.IsSuccess && result.Value.Verified)
        {
            sessionProof.MarkCurrentSessionVerified();
        }
        return result.ToApiResponse(this, ApiResponseCodes.Unauthorized);
    }

    [HttpPost("pin/update")]
    public async Task<IActionResult> ChangePin([FromBody] ChangeMfaPinRequest request, CancellationToken cancellationToken)
    {
        if (!TrySelectAuthDomain())
        {
            return Unauthorized();
        }

        var result = await commands.Send(new ChangeMfaPinCommand(request.OldPin, request.NewPin), cancellationToken);
        return result.ToApiResponse(this, ApiResponseCodes.Unauthorized);
    }

    private bool TrySelectAuthDomain()
    {
        string requestedDomain = Request.Headers[AuthDomainHeader].ToString().Trim().ToLowerInvariant();
        string portalCode;
        string[] allowedRoles;

        if (requestedDomain == "admin")
        {
            portalCode = PortalCodes.Admin;
            allowedRoles = ["HQ_ADMIN", "SCHOOL_ADMIN"];
        }
        else if (requestedDomain == "portal")
        {
            portalCode = PortalCodes.EService;
            allowedRoles = ["STUDENT"];
        }
        else
        {
            return false;
        }

        ClaimsIdentity? selectedIdentity = User.Identities.FirstOrDefault(identity =>
            identity.IsAuthenticated
            && identity.HasClaim(ClaimNames.Portal, portalCode)
            && identity.FindAll(ClaimNames.Role).Any(claim => allowedRoles.Contains(claim.Value, StringComparer.Ordinal)));

        if (selectedIdentity is null)
        {
            return false;
        }

        HttpContext.User = new ClaimsPrincipal(selectedIdentity);
        return true;
    }
}
