using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moe.Application.Abstractions.Messaging;
using Moe.Infrastructure.Shared.Security;
using Moe.Modules.EducationAccountTopUp.Application.TopUps.ChangeCampaignStatus;
using Moe.Modules.EducationAccountTopUp.Application.TopUps.CreateCampaign;
using Moe.Modules.EducationAccountTopUp.Application.TopUps.ExecuteRun;
using Moe.Modules.EducationAccountTopUp.Application.TopUps.PreviewCampaign;
using Moe.Modules.EducationAccountTopUp.Application.TopUps.UpdateCampaign;
using Moe.Modules.EducationAccountTopUp.Application.TopUps.UpsertCampaignRules;
using Moe.Modules.EducationAccountTopUp.Application.TopUps.UpsertFixedRecipients;
using Moe.Modules.EducationAccountTopUp.Contracts.TopUps.DTOs;

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
    /// Lists Top-Up Campaigns.
    /// </summary>
    [HttpGet]
    [Authorize(Policy = AuthorizationPolicies.ManageTopUps)]
    [ProducesResponseType(typeof(List<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCampaigns([FromServices] Moe.StudentFinance.Persistence.MoeDbContext dbContext, CancellationToken cancellationToken)
    {
        var campaigns = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync(
            dbContext.Set<Domain.TopUps.TopUpCampaign>()
                .OrderByDescending(c => c.Id),
            cancellationToken
        );
        return Ok(campaigns);
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
        
        if (result.IsFailure) return BadRequest(result.Error);
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

        if (result.IsFailure) return BadRequest(result.Error);
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
        if (id != commandRequest.TopUpCampaignId) return BadRequest();
        var result = await commandDispatcher.Send(commandRequest, cancellationToken);

        if (result.IsFailure) return BadRequest(result.Error);
        return NoContent();
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
        if (id != commandRequest.TopUpCampaignId) return BadRequest();
        var result = await commandDispatcher.Send(commandRequest, cancellationToken);

        if (result.IsFailure) return BadRequest(result.Error);
        return NoContent();
    }

    /// <summary>
    /// Upserts explicitly selected recipient accounts for a FIXED_SELECTION campaign.
    /// </summary>
    /// <param name="id">Campaign ID.</param>
    /// <param name="commandRequest">The fixed recipient payload.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [HttpPut("{id:long}/fixed-recipients")]
    [Authorize(Policy = AuthorizationPolicies.ManageTopUps)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpsertFixedRecipients(
        long id,
        [FromBody] UpsertFixedRecipientsCommand commandRequest,
        CancellationToken cancellationToken)
    {
        if (id != commandRequest.TopUpCampaignId) return BadRequest();
        var result = await commandDispatcher.Send(commandRequest, cancellationToken);

        if (result.IsFailure) return BadRequest(result.Error);
        return NoContent();
    }

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

        if (result.IsFailure) return BadRequest(result.Error);
        return Ok(result.Value);
    }

    /// <summary>
    /// Locks the campaign and dispatches it to the async execution workers.
    /// </summary>
    /// <param name="id">Campaign ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The generated TopUpRun ID.</returns>
    [HttpPost("{id:long}/execute")]
    [Authorize(Policy = AuthorizationPolicies.ManageTopUps)]
    [ProducesResponseType(typeof(long), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Execute(long id, CancellationToken cancellationToken)
    {
        var command = new ExecuteTopUpRunCommand(id);
        var result = await commandDispatcher.Send(command, cancellationToken);

        if (result.IsFailure) return BadRequest(result.Error);
        return Ok(result.Value);
    }

    /// <summary>
    /// Retrieves basic campaign information.
    /// </summary>
    /// <param name="id">Campaign ID.</param>
    /// <returns>Campaign basic data.</returns>
    [HttpGet("{id:long}", Name = "Get")]
    [Authorize(Policy = AuthorizationPolicies.ManageTopUps)]
    public IActionResult Get(long id) => Ok(new { Id = id });
}
