using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Http;
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

    [HttpPost("session")]
    [Authorize(Policy = AuthorizationPolicies.AdminPortal)]
    public IActionResult EstablishSession()
    {
        string? bearer = Request.Headers.Authorization.ToString();

        if (string.IsNullOrWhiteSpace(bearer)
            || !bearer.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return ApiResponseFactory.Failure(
                new("IDENTITY.ADMIN_TOKEN_REQUIRED", "A validated Microsoft Entra ID bearer token is required."),
                ApiResponseCodes.Unauthorized,
                HttpContext.TraceIdentifier);
        }

        Response.Cookies.Append(AuthenticationCookies.AdminSession, bearer["Bearer ".Length..].Trim(), new CookieOptions
        {
            HttpOnly = true,
            SameSite = Request.IsHttps ? SameSiteMode.None : SameSiteMode.Lax,
            Secure = Request.IsHttps,
            Path = "/",
            MaxAge = TimeSpan.FromMinutes(60)
        });

        return ApiResponseFactory.Ok(new { signedIn = true }, HttpContext.TraceIdentifier);
    }

    [HttpPost("logout")]
    [Authorize(Policy = AuthorizationPolicies.AdminPortal)]
    public IActionResult Logout()
    {
        Response.Cookies.Delete(AuthenticationCookies.AdminSession, new CookieOptions
        {
            HttpOnly = true,
            SameSite = Request.IsHttps ? SameSiteMode.None : SameSiteMode.Lax,
            Secure = Request.IsHttps,
            Path = "/"
        });

        return ApiResponseFactory.Ok(new { signedOut = true }, HttpContext.TraceIdentifier);
    }
}
