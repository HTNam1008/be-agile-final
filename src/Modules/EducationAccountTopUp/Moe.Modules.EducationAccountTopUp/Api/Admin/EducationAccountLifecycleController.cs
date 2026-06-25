using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moe.Application.Abstractions.Clock;
using Moe.Application.Abstractions.Messaging;
using Moe.Infrastructure.Shared.Api;
using Moe.Infrastructure.Shared.Security;
using Moe.Modules.EducationAccountTopUp.Application.Lifecycle;
using Moe.Modules.EducationAccountTopUp.Application.Lifecycle.RunHistory;
using Moe.Modules.EducationAccountTopUp.Domain.Lifecycle;
using Moe.SharedKernel.Results;

namespace Moe.Modules.EducationAccountTopUp.Api.Admin;

[ApiController]
[ApiVersion(1.0)]
[Route("api/admin/v{version:apiVersion}/education-account-lifecycle")]
[Authorize(Policy = AuthorizationPolicies.AdminPortal)]
[EnableCors("AdminCors")]
public sealed class EducationAccountLifecycleController(
    EducationAccountLifecycleWorker lifecycleWorker,
    IClock clock,
    IQueryDispatcher queries) : ControllerBase
{
    [HttpPost("run-now")]
    [Authorize(Policy = AuthorizationPolicies.LifecycleManualTrigger)]
    [ProducesResponseType(typeof(ApiResponse<EducationAccountLifecycleRunNowResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> RunNow(CancellationToken cancellationToken)
    {
        DateTimeOffset runAtUtc = clock.UtcNow;
        DateOnly today = DateOnly.FromDateTime(runAtUtc.UtcDateTime);
        EducationAccountLifecycleRunResult result = await lifecycleWorker.ProcessAsync(
            today,
            runAtUtc,
            EducationAccountLifecycleRunTriggerTypes.Manual,
            cancellationToken);

        return ApiResponseFactory.Ok(
            new EducationAccountLifecycleRunNowResponse(
                result.OpenedCount,
                result.ClosedCount,
                runAtUtc),
            HttpContext.TraceIdentifier);
    }

    [HttpGet("runs")]
    [ProducesResponseType(typeof(ApiResponse<PageResponse<EducationAccountLifecycleRunListItem>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetRuns(
        [FromQuery] EducationAccountLifecycleRunsRequest request,
        CancellationToken cancellationToken)
    {
        Result<PageResponse<EducationAccountLifecycleRunListItem>> result =
            await queries.Send(
                new GetEducationAccountLifecycleRunsQuery(
                    request.FromDate,
                    request.ToDate,
                    request.Page,
                    request.PageSize),
                cancellationToken);

        return ToLifecycleResponse(result, "Education Account lifecycle runs retrieved.");
    }

    [HttpGet("runs/{runId:long}")]
    [ProducesResponseType(typeof(ApiResponse<EducationAccountLifecycleRunDetail>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetRunDetail(
        long runId,
        CancellationToken cancellationToken)
    {
        Result<EducationAccountLifecycleRunDetail> result =
            await queries.Send(
                new GetEducationAccountLifecycleRunDetailQuery(runId),
                cancellationToken);

        return ToLifecycleResponse(result, "Education Account lifecycle run retrieved.");
    }

    private IActionResult ToLifecycleResponse<T>(Result<T> result, string message)
    {
        if (result.IsFailure)
        {
            return TopUpErrorResponseMapper.ToFailureResponse(result.Error, HttpContext);
        }

        return ApiResponseFactory.Ok(
            result.Value,
            HttpContext.TraceIdentifier,
            message);
    }
}

public sealed class EducationAccountLifecycleRunsRequest
{
    public DateOnly? FromDate { get; init; }
    public DateOnly? ToDate { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}

public sealed record EducationAccountLifecycleRunNowResponse(
    int OpenedCount,
    int ClosedCount,
    DateTimeOffset RunAtUtc);
