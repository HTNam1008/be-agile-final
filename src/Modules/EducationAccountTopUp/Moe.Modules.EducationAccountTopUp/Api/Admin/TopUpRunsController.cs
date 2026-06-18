using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moe.Application.Abstractions.Messaging;
using Moe.Infrastructure.Shared.Api;
using Moe.Infrastructure.Shared.Security;
using Moe.Modules.EducationAccountTopUp.Application.RunExecution.RequestManualRun;
using Moe.Modules.EducationAccountTopUp.Domain.TopUps;
using Moe.SharedKernel.Results;

namespace Moe.Modules.EducationAccountTopUp.Api.Admin;

[ApiController]
[ApiVersion(1.0)]
[Route("api/admin/v{version:apiVersion}/campaigns/{campaignId:long}/runs")]
[Authorize(Policy = AuthorizationPolicies.AdminPortal)]
[EnableCors("AdminCors")]
public sealed class TopUpRunsController(ICommandDispatcher commands) : ControllerBase
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
            return ToFailureResponse(result.Error);
        }

        Response.Headers.Location = $"/api/admin/v1/campaigns/{campaignId}/runs/{result.Value.RunId}";
        return ApiResponseFactory.Accepted(
            result.Value,
            HttpContext.TraceIdentifier,
            "Run request accepted");
    }

    private ObjectResult ToFailureResponse(Error error)
    {
        int statusCode = error == TopUpErrors.CampaignNotFound
            ? ApiResponseCodes.NotFound
            : ApiResponseCodes.BadRequest;

        return ApiResponseFactory.Failure(error, statusCode, HttpContext.TraceIdentifier);
    }
}
