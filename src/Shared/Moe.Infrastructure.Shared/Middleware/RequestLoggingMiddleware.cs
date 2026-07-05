using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moe.Infrastructure.Shared.Security;

namespace Moe.Infrastructure.Shared.Middleware;

public sealed class RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
{
    private static readonly HashSet<string> SensitiveHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "Authorization",
        "Cookie",
        "Set-Cookie",
        "X-Api-Key",
        "Proxy-Authorization"
    };

    public async Task InvokeAsync(HttpContext context)
    {
        logger.LogInformation(
            "Request started {Method} {Path}{Query}. TraceId={TraceId}",
            context.Request.Method,
            context.Request.Path,
            context.Request.QueryString,
            context.TraceIdentifier);

        foreach (var header in context.Request.Headers)
        {
            string value = SensitiveHeaders.Contains(header.Key) ? "[REDACTED]" : header.Value.ToString();
            logger.LogDebug("Header {HeaderKey}: {HeaderValue}", header.Key, value);
        }

        Stopwatch stopwatch = Stopwatch.StartNew();

        try
        {
            await next(context);
        }
        finally
        {
            stopwatch.Stop();
            logger.LogInformation(
                "HTTP {Method} {Path} => {StatusCode} in {ElapsedMs} ms. TraceId={TraceId}; Route={Route}; UserAccountId={UserAccountId}; Portal={Portal}; Roles={Roles}",
                context.Request.Method,
                context.Request.Path,
                context.Response.StatusCode,
                stopwatch.ElapsedMilliseconds,
                context.TraceIdentifier,
                context.GetEndpoint()?.DisplayName ?? string.Empty,
                context.User.FindFirst(ClaimNames.UserAccountId)?.Value ?? string.Empty,
                context.User.FindFirst(ClaimNames.Portal)?.Value ?? string.Empty,
                string.Join(",", context.User.FindAll(ClaimNames.Role).Select(claim => claim.Value).Distinct()));
        }
    }
}
