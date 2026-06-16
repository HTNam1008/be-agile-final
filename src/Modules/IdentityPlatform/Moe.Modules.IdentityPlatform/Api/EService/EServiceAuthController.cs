using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Moe.Application.Abstractions.Messaging;
using Moe.Infrastructure.Shared.Configuration;
using Moe.Infrastructure.Shared.Api;
using Moe.Modules.IdentityPlatform.Domain.Iam;
using Moe.Modules.IdentityPlatform.Application.Authentication.GetEServiceAuthFlow;
using Moe.Modules.IdentityPlatform.IGateway.Authentication;

namespace Moe.Modules.IdentityPlatform.Api.EService;

[ApiController]
[ApiVersion(1.0)]
[Route("api/eservice/v{version:apiVersion}/auth")]
[EnableCors("EServiceCors")]
public sealed class EServiceAuthController(
    IQueryDispatcher queries,
    ISingpassLoginGateway singpassLogin,
    IExternalIdentityProvisioningRepository provisionedIdentities,
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
            if (!await provisionedIdentities.HasActiveExternalIdentityAsync(
                IdentityProviderCodes.Singpass,
                login.ExternalIssuer,
                login.ExternalSubjectId,
                cancellationToken))
            {
                return RedirectWithError(
                    portalRedirectUri,
                    $"Singpass user is not provisioned in MOE. subject={login.ExternalSubjectId}; nric={login.IdentityNumber}; issuer={login.ExternalIssuer}");
            }

            string token = singpassLogin.IssueLocalApiToken(login);
            return RedirectWithToken(portalRedirectUri, new EServiceTokenResult(
                token,
                "Bearer",
                options.Value.EServiceSingpass.LocalTokenLifetimeMinutes * 60,
                login.ExternalSubjectId,
                login.IdentityNumber,
                login.DisplayName));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return RedirectWithError(portalRedirectUri, ex.Message);
        }
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

    private static RedirectResult RedirectWithToken(string portalRedirectUri, EServiceTokenResult token)
    {
        UriBuilder builder = new(portalRedirectUri);
        string separator = string.IsNullOrWhiteSpace(builder.Query) ? string.Empty : "&";
        builder.Query = string.Concat(
            builder.Query.TrimStart('?'),
            separator,
            "eservice_token=",
            Uri.EscapeDataString(token.AccessToken),
            "&eservice_token_type=",
            Uri.EscapeDataString(token.TokenType),
            "&eservice_expires_in=",
            token.ExpiresIn.ToString(System.Globalization.CultureInfo.InvariantCulture),
            "&eservice_subject=",
            Uri.EscapeDataString(token.ExternalSubjectId),
            "&eservice_nric=",
            Uri.EscapeDataString(token.IdentityNumber),
            "&eservice_name=",
            Uri.EscapeDataString(token.DisplayName));
        return new RedirectResult(builder.Uri.ToString());
    }
}
