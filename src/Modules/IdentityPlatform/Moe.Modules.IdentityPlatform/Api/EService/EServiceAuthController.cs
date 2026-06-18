using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Moe.Application.Abstractions.Messaging;
using Moe.Infrastructure.Shared.Configuration;
using Moe.Infrastructure.Shared.Api;
using Moe.Modules.IdentityPlatform.Application.Authentication.GetEServiceAuthFlow;
using Moe.Modules.IdentityPlatform.IGateway.Authentication;
using Moe.Infrastructure.Shared.Security;

namespace Moe.Modules.IdentityPlatform.Api.EService;

[ApiController]
[ApiVersion(1.0)]
[Route("api/eservice/v{version:apiVersion}/auth")]
[EnableCors("EServiceCors")]
public sealed class EServiceAuthController(
    IQueryDispatcher queries,
    ISingpassLoginGateway singpassLogin,
    IEServiceLoginResolver loginResolver,
    IOptions<AuthenticationOptions> options) : ControllerBase
{
    [HttpGet("flow")]
    [AllowAnonymous]
    public async Task<IActionResult> GetFlow(CancellationToken cancellationToken)
    {
        var result = await queries.Send(new GetEServiceAuthFlowQuery(), cancellationToken);
        return ApiResponseFactory.Ok(result.Value, HttpContext.TraceIdentifier);
    }

    [HttpGet("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login(CancellationToken cancellationToken)
    {
        SingpassLoginStartResult result = await singpassLogin.StartLoginAsync(cancellationToken);
        return Redirect(result.AuthorizationUrl);
    }

    [HttpGet("callback")]
    [AllowAnonymous]
    public async Task<IActionResult> Callback(
        [FromQuery] string? code,
        [FromQuery] string? state,
        [FromQuery] string? error,
        [FromQuery(Name = "error_description")] string? errorDescription,
        CancellationToken cancellationToken)
    {
        string portalRedirectUri = ResolvePortalRedirectUri(options.Value.EServiceSingpass);

        if (!string.IsNullOrWhiteSpace(error))
        {
            return RedirectWithError(portalRedirectUri, errorDescription ?? error);
        }

        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(state))
        {
            return RedirectWithError(portalRedirectUri, "Singpass did not return both code and state.");
        }

        try
        {
            SingpassLoginResult login = await singpassLogin.CompleteLoginAsync(code, state, cancellationToken);
            await loginResolver.ResolveAsync(login, cancellationToken);
            string token = singpassLogin.IssueLocalApiToken(login);
            WriteSessionCookie(token, options.Value.EServiceSingpass);
            return RedirectWithSuccess(portalRedirectUri);
        }
        catch (UnauthorizedAccessException ex)
        {
            return RedirectWithError(portalRedirectUri, ex.Message);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return RedirectWithError(portalRedirectUri, "Singpass login could not be completed.");
        }
    }

    [HttpPost("logout")]
    [Authorize(Policy = AuthorizationPolicies.EServicePortal)]
    public IActionResult Logout()
    {
        Response.Cookies.Delete(AuthenticationCookies.EServiceSession, new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Lax,
            Secure = Request.IsHttps,
            Path = "/"
        });

        return ApiResponseFactory.Ok(new { signedOut = true }, HttpContext.TraceIdentifier);
    }

    private static string ResolvePortalRedirectUri(SingpassSchemeOptions options)
        => string.IsNullOrWhiteSpace(options.PortalRedirectUri)
            ? "http://localhost:5173/?portal=eservice"
            : options.PortalRedirectUri;

    private static RedirectResult RedirectWithError(string portalRedirectUri, string error)
    {
        UriBuilder builder = new(portalRedirectUri);
        string separator = string.IsNullOrWhiteSpace(builder.Query) ? string.Empty : "&";
        builder.Query = $"{builder.Query.TrimStart('?')}{separator}eservice_error={Uri.EscapeDataString(error)}";
        return new RedirectResult(builder.Uri.ToString());
    }

    private static RedirectResult RedirectWithSuccess(string portalRedirectUri)
    {
        UriBuilder builder = new(portalRedirectUri);
        string separator = string.IsNullOrWhiteSpace(builder.Query) ? string.Empty : "&";
        builder.Query = $"{builder.Query.TrimStart('?')}{separator}eservice_login=success";
        return new RedirectResult(builder.Uri.ToString());
    }

    private void WriteSessionCookie(string token, SingpassSchemeOptions singpass)
    {
        Response.Cookies.Append(AuthenticationCookies.EServiceSession, token, new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Lax,
            Secure = Request.IsHttps,
            Path = "/",
            MaxAge = TimeSpan.FromMinutes(singpass.LocalTokenLifetimeMinutes)
        });
    }
}
