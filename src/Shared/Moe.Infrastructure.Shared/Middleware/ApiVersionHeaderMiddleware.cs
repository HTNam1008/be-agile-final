using Microsoft.AspNetCore.Http;

namespace Moe.Infrastructure.Shared.Middleware;

public sealed class ApiVersionHeaderMiddleware(RequestDelegate next)
{
    public const string ApiVersionHeader = "X-Api-Version";
    public const string SupportedVersionsHeader = "X-Supported-Api-Versions";

    public async Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[ApiVersionHeader] = ResolveRequestedVersion(context);
            context.Response.Headers[SupportedVersionsHeader] = "1.0";

            return Task.CompletedTask;
        });

        await next(context);
    }

    private static string ResolveRequestedVersion(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue(ApiVersionHeader, out var headerValue)
            && !string.IsNullOrWhiteSpace(headerValue))
        {
            return headerValue.ToString();
        }

        string? path = context.Request.Path.Value;

        if (string.IsNullOrWhiteSpace(path))
        {
            return "1.0";
        }

        string? versionSegment = path
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault(segment => segment.StartsWith("v", StringComparison.OrdinalIgnoreCase));

        if (string.IsNullOrWhiteSpace(versionSegment))
        {
            return "1.0";
        }

        string rawVersion = versionSegment.TrimStart('v', 'V');
        return rawVersion.Contains('.') ? rawVersion : $"{rawVersion}.0";
    }
}
