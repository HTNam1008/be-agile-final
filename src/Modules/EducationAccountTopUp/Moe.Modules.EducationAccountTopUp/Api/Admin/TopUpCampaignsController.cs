using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moe.Application.Abstractions.Messaging;
using Moe.Infrastructure.Shared.Api;
using Moe.Infrastructure.Shared.Security;
using Moe.Modules.EducationAccountTopUp.Application.TopUps.ChangeCampaignStatus;
using Moe.Modules.EducationAccountTopUp.Application.TopUps.CreateCampaign;
using Moe.Modules.EducationAccountTopUp.Application.TopUps.GetCampaignRules;
using Moe.Modules.EducationAccountTopUp.Application.TopUps.GetCampaigns;
using Moe.Modules.EducationAccountTopUp.Application.TopUps.GetFixedRecipients;
using Moe.Modules.EducationAccountTopUp.Application.TopUps.PreviewCampaign;
using Moe.Modules.EducationAccountTopUp.Application.TopUps.UpdateCampaign;
using Moe.Modules.EducationAccountTopUp.Application.TopUps.UpsertCampaignRules;
using Moe.Modules.EducationAccountTopUp.Application.TopUps.UpsertFixedRecipients;
using Moe.Modules.EducationAccountTopUp.Contracts.TopUps.DTOs;
using Moe.SharedKernel.Results;

namespace Moe.Modules.EducationAccountTopUp.Api.Admin;

[ApiController]
[ApiVersion("1.0")]
[Route("api/admin/v{version:apiVersion}/top-up-campaigns")]
[Authorize(Policy = AuthorizationPolicies.AdminPortal)]
[EnableCors("AdminCors")]
public sealed class TopUpCampaignsController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher) : ControllerBase
{
    /// <summary>
    /// Lists Top-Up Campaigns visible to the authenticated user.
    /// </summary>
    [HttpGet]
    [Authorize(Policy = AuthorizationPolicies.ManageTopUps)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCampaigns(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? search = null,
        [FromQuery] string? status = null,
        [FromQuery] string? dateFrom = null,
        [FromQuery] string? dateTo = null,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.Send(
            new GetCampaignsQuery(pageNumber, pageSize, search, status, dateFrom, dateTo),
            cancellationToken);
        if (result.IsFailure) return TopUpErrorResponseMapper.ToFailureResponse(result.Error, HttpContext);
        return Ok(result.Value);
    }

    /// <summary>
    /// Creates a new Top-Up Campaign definition.
    /// </summary>
    /// <param name="request">The campaign definition data.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The ID of the newly created campaign.</returns>
    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.ManageTopUps)]
    [ProducesResponseType(typeof(long), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Create(
        [FromBody] CreateCampaignRequest request,
        CancellationToken cancellationToken)
    {
        var command = new CreateCampaignCommand(request);
        var result = await commandDispatcher.Send(command, cancellationToken);

        if (result.IsFailure) return TopUpErrorResponseMapper.ToFailureResponse(result.Error, HttpContext);
        return CreatedAtAction(nameof(Get), new { id = result.Value }, result.Value);
    }

    /// <summary>
    /// Updates an existing Top-Up Campaign in DRAFT status.
    /// </summary>
    /// <param name="id">Campaign ID.</param>
    /// <param name="request">The updated campaign definition.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [HttpPut("{id:long}")]
    [Authorize(Policy = AuthorizationPolicies.ManageTopUps)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        long id,
        [FromBody] UpdateCampaignRequest request,
        CancellationToken cancellationToken)
    {
        var command = new UpdateCampaignCommand(id, request);
        var result = await commandDispatcher.Send(command, cancellationToken);

        if (result.IsFailure) return TopUpErrorResponseMapper.ToFailureResponse(result.Error, HttpContext);
        return NoContent();
    }

    /// <summary>
    /// Changes the status of a Campaign (e.g., DRAFT to READY).
    /// </summary>
    /// <param name="id">Campaign ID.</param>
    /// <param name="commandRequest">The status change command.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [HttpPatch("{id:long}/status")]
    [Authorize(Policy = AuthorizationPolicies.ManageTopUps)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ChangeStatus(
        long id,
        [FromBody] ChangeCampaignStatusCommand commandRequest,
        CancellationToken cancellationToken)
    {
        if (id != commandRequest.TopUpCampaignId)
            return ApiResponseFactory.Failure(new Error("Campaign.IdMismatch", "URL ID does not match Payload ID."), ApiResponseCodes.BadRequest, HttpContext.TraceIdentifier);
        var result = await commandDispatcher.Send(commandRequest, cancellationToken);

        if (result.IsFailure) return TopUpErrorResponseMapper.ToFailureResponse(result.Error, HttpContext);
        return NoContent();
    }

    /// <summary>
    /// Retrieves dynamic rules for a campaign.
    /// </summary>
    [HttpGet("{id:long}/rules")]
    [Authorize(Policy = AuthorizationPolicies.ManageTopUps)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRules(long id, CancellationToken cancellationToken)
    {
        var result = await queryDispatcher.Send(new GetCampaignRulesQuery(id), cancellationToken);
        if (result.IsFailure) return TopUpErrorResponseMapper.ToFailureResponse(result.Error, HttpContext);
        return Ok(result.Value);
    }

    /// <summary>
    /// Upserts dynamic rules for a DYNAMIC_RULES campaign.
    /// </summary>
    /// <param name="id">Campaign ID.</param>
    /// <param name="commandRequest">The list of rules to apply.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [HttpPut("{id:long}/rules")]
    [Authorize(Policy = AuthorizationPolicies.ManageTopUps)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpsertRules(
        long id,
        [FromBody] UpsertCampaignRulesCommand commandRequest,
        CancellationToken cancellationToken)
    {
        if (id != commandRequest.TopUpCampaignId)
            return ApiResponseFactory.Failure(new Error("Campaign.IdMismatch", "URL ID does not match Payload ID."), ApiResponseCodes.BadRequest, HttpContext.TraceIdentifier);
        var result = await commandDispatcher.Send(commandRequest, cancellationToken);

        if (result.IsFailure) return TopUpErrorResponseMapper.ToFailureResponse(result.Error, HttpContext);
        return NoContent();
    }

    /// <summary>
    /// Retrieves fixed recipients for a campaign.
    /// </summary>
    [HttpGet("{id:long}/fixed-recipients")]
    [Authorize(Policy = AuthorizationPolicies.ManageTopUps)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetFixedRecipients(long id, CancellationToken cancellationToken)
    {
        var result = await queryDispatcher.Send(new GetFixedRecipientsQuery(id), cancellationToken);
        if (result.IsFailure) return TopUpErrorResponseMapper.ToFailureResponse(result.Error, HttpContext);
        return Ok(result.Value);
    }

    /// <summary>
    /// Upserts explicitly selected recipient accounts for a FIXED_SELECTION campaign.
    /// </summary>
    /// <param name="id">Campaign ID.</param>
    /// <param name="commandRequest">The fixed recipient payload.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [HttpPut("{id:long}/fixed-recipients")]
    [Authorize(Policy = AuthorizationPolicies.ManageTopUps)]
    [ProducesResponseType(typeof(ApiResponse<UpsertFixedRecipientsResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpsertFixedRecipients(
        long id,
        [FromBody] UpsertFixedRecipientsRequest request,
        CancellationToken cancellationToken)
    {
        var command = new UpsertFixedRecipientsCommand(
            id,
            request.Mode,
            request.Filter,
            request.Recipients,
            request.ExcludedEducationAccountIds);

        var result = await commandDispatcher.Send(command, cancellationToken);

        if (result.IsFailure)
        {
            return ApiResponseFactory.Failure(
                result.Error,
                GetRecipientSelectionFailureStatusCode(result.Error.Code),
                HttpContext.TraceIdentifier);
        }

        return ApiResponseFactory.Ok(
            result.Value,
            HttpContext.TraceIdentifier,
            "Recipients updated successfully.");
    }

    private static int GetRecipientSelectionFailureStatusCode(string errorCode)
        => errorCode switch
        {
            "TOPUP.CAMPAIGN_NOT_FOUND" => ApiResponseCodes.NotFound,
            "TOPUP.ADMIN_ORGANIZATION_SCOPE_REQUIRED" => ApiResponseCodes.Forbidden,
            "TOPUP.ORGANIZATION_OUTSIDE_SCOPE" => ApiResponseCodes.Forbidden,
            "TOPUP.ACCOUNT_SELECTION_OUTSIDE_SCOPE" => ApiResponseCodes.Forbidden,
            _ => ApiResponseCodes.BadRequest
        };

    /// <summary>
    /// Previews the financial projection of a campaign (paginated, non-mutating).
    /// </summary>
    /// <param name="id">Campaign ID.</param>
    /// <param name="request">Pagination parameters.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A paginated projection including total matched count and estimated financial impact.</returns>
    [HttpPost("{id:long}/preview")]
    [Authorize(Policy = AuthorizationPolicies.ManageTopUps)]
    [ProducesResponseType(typeof(PreviewCampaignResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Preview(
        long id,
        [FromBody] PreviewCampaignRequest request,
        CancellationToken cancellationToken)
    {
        var query = new PreviewCampaignQuery(id, request.PageNumber, request.PageSize);
        var result = await queryDispatcher.Send(query, cancellationToken);

        if (result.IsFailure) return TopUpErrorResponseMapper.ToFailureResponse(result.Error, HttpContext);
        return Ok(result.Value);
    }


    /// <summary>
    /// Retrieves basic campaign information.
    /// </summary>
    /// <param name="id">Campaign ID.</param>
    /// <returns>Campaign basic data.</returns>
    [HttpGet("{id:long}", Name = "Get")]
    [Authorize(Policy = AuthorizationPolicies.ManageTopUps)]
    public async Task<IActionResult> Get(long id, CancellationToken cancellationToken)
    {
        var result = await queryDispatcher.Send(new GetCampaignByIdQuery(id), cancellationToken);
        return result.IsSuccess
            ? result.Value is not null ? Ok(result.Value) : NotFound()
            : BadRequest(result.Error);
    }
}
