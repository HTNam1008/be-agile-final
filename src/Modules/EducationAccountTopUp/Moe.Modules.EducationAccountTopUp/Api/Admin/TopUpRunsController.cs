using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moe.Application.Abstractions.Messaging;
using Moe.Infrastructure.Shared.Api;
using Moe.Infrastructure.Shared.Security;
using Moe.Modules.EducationAccountTopUp.Application.History;
using Moe.Modules.EducationAccountTopUp.Application.RunExecution;
using Moe.Modules.EducationAccountTopUp.Application.RunExecution.GetRunSummary;
using Moe.Modules.EducationAccountTopUp.Application.RunExecution.RequestManualRun;
using Moe.Modules.EducationAccountTopUp.Application.RunExecution.CancelRun;
using Moe.Modules.EducationAccountTopUp.Domain.TopUps;
using Moe.SharedKernel.Results;

namespace Moe.Modules.EducationAccountTopUp.Api.Admin;

[ApiController]
[ApiVersion(1.0)]
[Route("api/admin/v{version:apiVersion}/top-up-campaigns/{campaignId:long}/runs")]
[Authorize(Policy = AuthorizationPolicies.AdminPortal)]
[EnableCors("AdminCors")]
public sealed class TopUpRunsController(
    ICommandDispatcher commands,
    IQueryDispatcher queries) : ControllerBase
{
    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.ManageTopUps)]
    [ProducesResponseType(typeof(ApiResponse<RequestManualRunResponse>), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RequestManualRun(
        long campaignId,
        [FromBody] RequestManualRunRequest request,
        CancellationToken cancellationToken)
    {
        RequestManualRunCommand command = new(campaignId, request.IdempotencyKey, request.Note);

        Result<RequestManualRunResponse> result = await commands.Send(command, cancellationToken);
        if (result.IsFailure)
        {
            return TopUpErrorResponseMapper.ToFailureResponse(result.Error, HttpContext);
        }

        Response.Headers.Location = $"/api/admin/v1/top-up/runs/{result.Value.RunId}";
        return ApiResponseFactory.Accepted(
            result.Value,
            HttpContext.TraceIdentifier,
            "Run request accepted");
    }

    [HttpPost("{runId:long}/reconcile")]
    [Authorize(Policy = AuthorizationPolicies.ManageTopUps)]
    [ProducesResponseType(typeof(ApiResponse<ReconciliationResult>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ReconcileRun(
        long campaignId,
        long runId,
        [FromServices] IRunReconciliationService reconciliationService,
        CancellationToken cancellationToken)
    {
        Result<RunSummaryResponse> accessResult = await queries.Send(
            new GetRunSummaryQuery(runId, campaignId),
            cancellationToken);

        if (accessResult.IsFailure)
        {
            return TopUpErrorResponseMapper.ToFailureResponse(accessResult.Error, HttpContext);
        }

        Result<ReconciliationResult> result = await reconciliationService.ReconcileRunAsync(
            runId,
            cancellationToken);

        return result.IsFailure
            ? TopUpErrorResponseMapper.ToFailureResponse(result.Error, HttpContext)
            : ApiResponseFactory.Ok(
                result.Value,
                HttpContext.TraceIdentifier,
                $"Reconciliation: {result.Value.ReconciliationStatus}");
    }

    [HttpPost("{runId:long}/cancel")]
    [Authorize(Policy = AuthorizationPolicies.ManageTopUps)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CancelRun(
        long campaignId,
        long runId,
        CancellationToken cancellationToken)
    {
        Result<RunSummaryResponse> accessResult = await queries.Send(
            new GetRunSummaryQuery(runId, campaignId),
            cancellationToken);

        if (accessResult.IsFailure)
        {
            return TopUpErrorResponseMapper.ToFailureResponse(accessResult.Error, HttpContext);
        }

        Result result = await commands.Send(new CancelTopUpRunCommand(runId), cancellationToken);

        return result.IsFailure
            ? TopUpErrorResponseMapper.ToFailureResponse(result.Error, HttpContext)
            : ApiResponseFactory.Ok(
                new { },
                HttpContext.TraceIdentifier,
                "Run cancelled successfully.");
    }
}
