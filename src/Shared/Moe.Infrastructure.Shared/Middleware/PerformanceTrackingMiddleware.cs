using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Moe.Infrastructure.Shared.Middleware;

public sealed class PerformanceTrackingMiddleware(
    RequestDelegate next,
    ILogger<PerformanceTrackingMiddleware> logger)
{
    private const string ResponseTimeHeader = "X-Response-Time-Ms";
    private const int SlowRequestThresholdMilliseconds = 1_000;

    public async Task InvokeAsync(HttpContext context)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        context.Response.OnStarting(() =>
        {
            stopwatch.Stop();
            context.Response.Headers[ResponseTimeHeader] = stopwatch.ElapsedMilliseconds.ToString();

            logger.LogInformation(
                "{Method} {Path} completed in {ElapsedMs} ms with {StatusCode}",
                context.Request.Method,
                context.Request.Path,
                stopwatch.ElapsedMilliseconds,
                context.Response.StatusCode);

            return Task.CompletedTask;
        });

        try
        {
            await next(context);
        }
        finally
        {
            if (stopwatch.ElapsedMilliseconds >= SlowRequestThresholdMilliseconds)
            {
                logger.LogWarning(
                    "Slow HTTP request {Method} {Path} completed in {ElapsedMs} ms. TraceId={TraceId}",
                    context.Request.Method,
                    context.Request.Path,
                    stopwatch.ElapsedMilliseconds,
                    context.TraceIdentifier);
            }
        }
    }
}
