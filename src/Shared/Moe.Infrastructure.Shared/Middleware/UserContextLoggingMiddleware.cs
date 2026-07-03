using System.Diagnostics;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moe.Infrastructure.Shared.Security;

namespace Moe.Infrastructure.Shared.Middleware;

public sealed class UserContextLoggingMiddleware(
    RequestDelegate next,
    ILogger<UserContextLoggingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        using (logger.BeginScope(BuildScope(context)))
        {
            await next(context);
        }
    }

    private static Dictionary<string, object> BuildScope(HttpContext context)
    {
        ClaimsPrincipal user = context.User;
        Activity? activity = Activity.Current;

        Dictionary<string, object> scope = new()
        {
            ["CorrelationId"] = context.TraceIdentifier,
            ["TraceId"] = activity?.TraceId.ToString() ?? context.TraceIdentifier,
            ["SpanId"] = activity?.SpanId.ToString() ?? string.Empty,
            ["HttpMethod"] = context.Request.Method,
            ["HttpPath"] = context.Request.Path.Value ?? string.Empty,
            ["Route"] = context.GetEndpoint()?.DisplayName ?? string.Empty
        };

        AddIfPresent(scope, "UserAccountId", user.FindFirstValue(ClaimNames.UserAccountId));
        AddIfPresent(scope, "PersonId", user.FindFirstValue(ClaimNames.PersonId));
        AddIfPresent(scope, "Portal", user.FindFirstValue(ClaimNames.Portal));
        AddIfPresent(scope, "IdentityProvider", user.FindFirstValue(ClaimNames.IdentityProvider));
        AddIfPresent(scope, "AuthScheme", user.FindFirstValue(LocalIdentityClaimNames.ExternalAuthenticationScheme));
        AddIfPresent(scope, "Roles", string.Join(",", user.FindAll(ClaimNames.Role).Select(claim => claim.Value).Distinct()));
        AddIfPresent(scope, "OrganizationUnitIds", string.Join(",", user.FindAll(ClaimNames.OrganizationUnitId).Select(claim => claim.Value).Distinct()));

        return scope;
    }

    private static void AddIfPresent(Dictionary<string, object> scope, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            scope[key] = value;
        }
    }
}
