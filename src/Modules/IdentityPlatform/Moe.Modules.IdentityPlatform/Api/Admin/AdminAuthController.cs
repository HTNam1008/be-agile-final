using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moe.Application.Abstractions.Audit;
using Moe.Application.Abstractions.Messaging;
using Moe.Application.Abstractions.Persistence;
using Moe.Application.Abstractions.Security;
using Moe.Infrastructure.Shared.Api;
using Moe.Infrastructure.Shared.Security;
using Moe.Modules.IdentityPlatform.Application.Authentication.GetAdminAuthFlow;
using Moe.Modules.IdentityPlatform.Application.Authentication.GetCurrentIdentity;
using Moe.Modules.IdentityPlatform.Application.Authentication.RecordAdminLogin;
using Moe.Modules.IdentityPlatform.IGateway.Authentication;

namespace Moe.Modules.IdentityPlatform.Api.Admin;

[ApiController]
[ApiVersion(1.0)]
[Route("api/admin/v{version:apiVersion}/auth")]
[EnableCors("AdminCors")]
public sealed class AdminAuthController(
    IQueryDispatcher queries,
    ICommandDispatcher commands,
    IAdminAccessControl adminAccess,
    IAuditService audit,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork) : ControllerBase
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
    public async Task<IActionResult> EstablishSession(CancellationToken cancellationToken)
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

        var loginResult = await commands.Send(new RecordAdminLoginCommand(), cancellationToken);
        if (loginResult.IsFailure)
        {
            return ApiResponseFactory.Failure(
                loginResult.Error,
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
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        Response.Cookies.Delete(AuthenticationCookies.AdminSession, new CookieOptions
        {
            HttpOnly = true,
            SameSite = Request.IsHttps ? SameSiteMode.None : SameSiteMode.Lax,
            Secure = Request.IsHttps,
            Path = "/"
        });

        await RecordLogoutAuditAsync(cancellationToken);
        return ApiResponseFactory.Ok(new { signedOut = true }, HttpContext.TraceIdentifier);
    }

    private async Task RecordLogoutAuditAsync(CancellationToken cancellationToken)
    {
        if (!adminAccess.IsSchoolAdmin || adminAccess.IsHqAdmin || currentUser.UserAccountId is not long userAccountId)
        {
            return;
        }

        foreach (long organizationId in adminAccess.ScopedOrganizationIds)
        {
            await audit.RecordSchoolActionAsync(
                new SchoolAuditContext(
                    AuditActionCodes.AdminLogout,
                    "UserAccount",
                    userAccountId,
                    organizationId,
                    new SchoolAuditDetails(
                        "Admin logout",
                        RelatedIds: new Dictionary<string, long>
                        {
                            ["userAccountId"] = userAccountId
                        },
                        ReasonCode: "ADMIN_AUTH")),
                cancellationToken);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
