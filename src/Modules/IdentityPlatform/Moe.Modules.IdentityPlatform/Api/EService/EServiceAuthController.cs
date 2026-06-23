using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
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
    IOptions<AuthenticationOptions> options,
    IOptions<PortalOptions> portalOptions,
    ILogger<EServiceAuthController> logger) : ControllerBase
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
    public async Task<IActionResult> Login([FromQuery] string? returnUrl, CancellationToken cancellationToken)
    {
        string? portalReturnUrl = ResolveAllowedReturnUrl(returnUrl, portalOptions.Value);
        SingpassLoginStartResult result = await singpassLogin.StartLoginAsync(portalReturnUrl, cancellationToken);
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
            portalRedirectUri = login.PortalRedirectUri ?? portalRedirectUri;
            await loginResolver.ResolveAsync(login, cancellationToken);
            string token = singpassLogin.IssueLocalApiToken(login);
            WriteSessionCookie(token, options.Value.EServiceSingpass);
            return RedirectWithSuccess(portalRedirectUri);
        }
        catch (UnauthorizedAccessException ex)
        {
            logger.LogWarning(
                ex,
                "Singpass login was rejected during callback. TraceId={TraceId}",
                HttpContext.TraceIdentifier);
            return RedirectWithError(portalRedirectUri, ex.Message);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(
                ex,
                "Singpass login callback failed. TraceId={TraceId}",
                HttpContext.TraceIdentifier);
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
            SameSite = Request.IsHttps ? SameSiteMode.None : SameSiteMode.Lax,
            Secure = Request.IsHttps,
            Path = "/"
        });

        return ApiResponseFactory.Ok(new { signedOut = true }, HttpContext.TraceIdentifier);
    }

    private static string ResolvePortalRedirectUri(SingpassSchemeOptions options)
        => string.IsNullOrWhiteSpace(options.PortalRedirectUri)
            ? "http://localhost:5173/?portal=eservice"
            : options.PortalRedirectUri;

    private static string? ResolveAllowedReturnUrl(string? returnUrl, PortalOptions portals)
    {
        if (string.IsNullOrWhiteSpace(returnUrl)
            || !Uri.TryCreate(returnUrl, UriKind.Absolute, out Uri? uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return null;
        }

        string origin = uri.IsDefaultPort
            ? $"{uri.Scheme}://{uri.Host}"
            : $"{uri.Scheme}://{uri.Host}:{uri.Port}";

        return portals.EServiceAllowedOrigins.Contains(origin, StringComparer.OrdinalIgnoreCase)
            ? uri.ToString()
            : null;
    }

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
            SameSite = Request.IsHttps ? SameSiteMode.None : SameSiteMode.Lax,
            Secure = Request.IsHttps,
            Path = "/",
            MaxAge = TimeSpan.FromMinutes(singpass.LocalTokenLifetimeMinutes)
        });
    }
}
