using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moe.Infrastructure.Shared.Middleware;

namespace Moe.Infrastructure.Shared.Observability;

public sealed class CorrelationIdDelegatingHandler(
    IHttpContextAccessor httpContextAccessor,
    ILogger<CorrelationIdDelegatingHandler> logger) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        string? correlationId = httpContextAccessor.HttpContext?.TraceIdentifier
            ?? Activity.Current?.TraceId.ToString();

        if (!string.IsNullOrWhiteSpace(correlationId)
            && !request.Headers.Contains(CorrelationIdMiddleware.HeaderName))
        {
            request.Headers.TryAddWithoutValidation(CorrelationIdMiddleware.HeaderName, correlationId);
        }

        Stopwatch stopwatch = Stopwatch.StartNew();

        try
        {
            HttpResponseMessage response = await base.SendAsync(request, cancellationToken);
            stopwatch.Stop();

            logger.LogInformation(
                "HTTP dependency {Method} {Host}{Path} => {StatusCode} in {ElapsedMs} ms. TraceId={TraceId}",
                request.Method.Method,
                request.RequestUri?.Host,
                request.RequestUri?.AbsolutePath,
                (int)response.StatusCode,
                stopwatch.ElapsedMilliseconds,
                correlationId ?? string.Empty);

            return response;
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            stopwatch.Stop();

            logger.LogError(
                exception,
                "HTTP dependency {Method} {Host}{Path} failed in {ElapsedMs} ms. TraceId={TraceId}",
                request.Method.Method,
                request.RequestUri?.Host,
                request.RequestUri?.AbsolutePath,
                stopwatch.ElapsedMilliseconds,
                correlationId ?? string.Empty);

            throw;
        }
    }
}
