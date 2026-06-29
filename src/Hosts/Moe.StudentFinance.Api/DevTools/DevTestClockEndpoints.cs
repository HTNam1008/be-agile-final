using Microsoft.Extensions.Options;
using Moe.Application.Abstractions.Clock;
using Moe.Infrastructure.Shared.Clock;
using Moe.Infrastructure.Shared.Security;
using Moe.Modules.AiCopilot.Infrastructure.Persistence;
using Moe.Modules.EducationAccountTopUp.Application.Lifecycle;
using Moe.Modules.EducationAccountTopUp.Application.RunExecution;

namespace Moe.StudentFinance.Api.DevTools;

internal static class DevTestClockEndpoints
{
    public static bool IsEnabled(WebApplicationBuilder builder)
        => builder.Environment.IsDevelopment()
           || builder.Configuration.GetValue<bool>("DevTools:TestClockEnabled");

    public static IEndpointRouteBuilder MapDevTestClockEndpoints(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints.MapGroup("/dev")
            .RequireCors("PortalCors");

        group.MapGet("/test-tools/status", (
            HttpContext httpContext,
            IWebHostEnvironment environment,
            IConfiguration configuration,
            DevelopmentManualClock clock,
            IOptions<EducationAccountLifecycleOptions> lifecycleOptions) =>
        {
            IResult? forbidden = RequireAccess(httpContext, environment);
            if (forbidden is not null) return forbidden;

            return Results.Ok(new
            {
                enabled = true,
                environment = environment.EnvironmentName,
                azureSafeMode = !environment.IsDevelopment(),
                warning = "Test clock affects this running API instance only. Use single-instance UAT/Staging for reliable worker tests.",
                clock = ClockState(clock),
                lifecycle = new
                {
                    lifecycleOptions.Value.Enabled,
                    lifecycleOptions.Value.RunAtUtc
                },
                actions = new[]
                {
                    new { code = "TOP_UP_SCHEDULER", label = "Run top-up scheduler once", method = "POST", href = "/dev/workers/top-up-scheduler/run-once" },
                    new { code = "EA_LIFECYCLE_IF_DUE", label = "Run lifecycle if due", method = "POST", href = "/dev/workers/education-account-lifecycle/run-if-due" },
                    new { code = "EA_LIFECYCLE_NOW", label = "Run lifecycle now", method = "POST", href = "/dev/workers/education-account-lifecycle/run-now" },
                    new { code = "AI_RETENTION", label = "Run AI retention cleanup", method = "POST", href = "/dev/workers/ai-retention/run-once" }
                }
            });
        }).AllowAnonymous();

        group.MapGet("/clock", (
            HttpContext httpContext,
            IWebHostEnvironment environment,
            DevelopmentManualClock clock) =>
        {
            IResult? forbidden = RequireAccess(httpContext, environment);
            return forbidden ?? Results.Ok(ClockState(clock));
        }).AllowAnonymous();

        group.MapPut("/clock", (
            SetDevelopmentClockRequest request,
            HttpContext httpContext,
            IWebHostEnvironment environment,
            DevelopmentManualClock clock) =>
        {
            IResult? forbidden = RequireAccess(httpContext, environment);
            if (forbidden is not null) return forbidden;

            clock.Set(request.UtcNow);
            return Results.Ok(ClockState(clock));
        }).AllowAnonymous();

        group.MapDelete("/clock", (
            HttpContext httpContext,
            IWebHostEnvironment environment,
            DevelopmentManualClock clock) =>
        {
            IResult? forbidden = RequireAccess(httpContext, environment);
            if (forbidden is not null) return forbidden;

            clock.Reset();
            return Results.Ok(ClockState(clock));
        }).AllowAnonymous();

        group.MapPost("/workers/top-up-scheduler/run-once", async (
            HttpContext httpContext,
            IWebHostEnvironment environment,
            DevelopmentManualClock clock,
            TopUpSchedulerWorker worker,
            CancellationToken cancellationToken) =>
        {
            IResult? forbidden = RequireAccess(httpContext, environment);
            if (forbidden is not null) return forbidden;

            return Results.Ok(await RunWorkerAsync(
                "TOP_UP_SCHEDULER",
                clock,
                async () =>
                {
                    TopUpSchedulerRunOnceResult result = await worker.RunOnceAsync(cancellationToken);
                    string message = result.CreatedRunCount > 0
                        ? $"Created and dispatched {result.CreatedRunCount} scheduled top-up run(s)."
                        : "No due top-up campaign runs were created.";
                    string status = result.FailedRunCount > 0 ? "FAILED" : "SUCCEEDED";
                    return WorkerResult(status, message, result);
                }));
        }).AllowAnonymous();

        group.MapPost("/workers/education-account-lifecycle/run-if-due", async (
            HttpContext httpContext,
            IWebHostEnvironment environment,
            DevelopmentManualClock clock,
            EducationAccountLifecycleWorker worker,
            CancellationToken cancellationToken) =>
        {
            IResult? forbidden = RequireAccess(httpContext, environment);
            if (forbidden is not null) return forbidden;

            return Results.Ok(await RunWorkerAsync(
                "EA_LIFECYCLE_IF_DUE",
                clock,
                async () =>
                {
                    EducationAccountLifecycleRunResult result = await worker.RunIfDueAsync(cancellationToken);
                    if (result.Skipped)
                    {
                        return WorkerResult("SKIPPED", result.SkipReason ?? "Lifecycle run was skipped.", result);
                    }

                    return WorkerResult(
                        "SUCCEEDED",
                        $"Lifecycle completed. Opened {result.OpenedCount} account(s), closed {result.ClosedCount} account(s).",
                        result);
                }));
        }).AllowAnonymous();

        group.MapPost("/workers/education-account-lifecycle/run-now", async (
            HttpContext httpContext,
            IWebHostEnvironment environment,
            DevelopmentManualClock clock,
            EducationAccountLifecycleWorker worker,
            CancellationToken cancellationToken) =>
        {
            IResult? forbidden = RequireAccess(httpContext, environment);
            if (forbidden is not null) return forbidden;

            return Results.Ok(await RunWorkerAsync(
                "EA_LIFECYCLE_NOW",
                clock,
                async () =>
                {
                    EducationAccountLifecycleRunResult result = await worker.RunNowAsync(cancellationToken);
                    return WorkerResult(
                        "SUCCEEDED",
                        $"Manual lifecycle completed. Opened {result.OpenedCount} account(s), closed {result.ClosedCount} account(s).",
                        result);
                }));
        }).AllowAnonymous();

        group.MapPost("/workers/ai-retention/run-once", async (
            HttpContext httpContext,
            IWebHostEnvironment environment,
            DevelopmentManualClock clock,
            AiRetentionCleanupRunner runner,
            CancellationToken cancellationToken) =>
        {
            IResult? forbidden = RequireAccess(httpContext, environment);
            if (forbidden is not null) return forbidden;

            return Results.Ok(await RunWorkerAsync(
                "AI_RETENTION",
                clock,
                async () =>
                {
                    AiRetentionCleanupResult result = await runner.RunOnceAsync(cancellationToken);
                    return WorkerResult(
                        "SUCCEEDED",
                        $"Deleted {result.DeletedCount} expired AI conversation(s).",
                        result);
                }));
        }).AllowAnonymous();

        return endpoints;
    }

    private static async Task<object> RunWorkerAsync(
        string workerCode,
        DevelopmentManualClock clock,
        Func<Task<WorkerOutcome>> action)
    {
        DateTimeOffset ranAtUtc = DateTimeOffset.UtcNow;
        DateTimeOffset clockUtcNow = clock.UtcNow;
        try
        {
            WorkerOutcome outcome = await action();
            return new
            {
                workerCode,
                ranAtUtc,
                clockUtcNow,
                outcome.Status,
                outcome.Message,
                data = outcome.Data
            };
        }
        catch (Exception exception)
        {
            return new
            {
                workerCode,
                ranAtUtc,
                clockUtcNow,
                status = "FAILED",
                message = exception.Message,
                data = new { exceptionType = exception.GetType().Name }
            };
        }
    }

    private static WorkerOutcome WorkerResult(string status, string message, object data)
        => new(status, message, data);

    private static object ClockState(DevelopmentManualClock clock)
        => new
        {
            utcNow = clock.UtcNow,
            isOverridden = clock.IsOverridden
        };

    private static IResult? RequireAccess(HttpContext httpContext, IWebHostEnvironment environment)
    {
        if (environment.IsDevelopment())
        {
            return null;
        }

        bool isHqAdmin = httpContext.User.Identity?.IsAuthenticated == true
                         && httpContext.User.HasClaim(ClaimNames.Role, "HQ_ADMIN");
        return isHqAdmin ? null : Results.Forbid();
    }
}

internal sealed record SetDevelopmentClockRequest(DateTimeOffset UtcNow);

internal sealed record WorkerOutcome(string Status, string Message, object Data);
