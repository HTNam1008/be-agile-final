using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moe.Application.Abstractions.Messaging;
using Moe.Modules.EducationAccountTopUp.Application.TopUps.ChangeCampaignStatus;
using Moe.Modules.EducationAccountTopUp.Application.TopUps.CreateCampaign;
using Moe.Modules.EducationAccountTopUp.Application.TopUps.ExecuteRun;
using Moe.Modules.EducationAccountTopUp.Application.TopUps.PreviewCampaign;
using Moe.Modules.EducationAccountTopUp.Application.TopUps.UpdateCampaign;
using Moe.Modules.EducationAccountTopUp.Application.TopUps.UpsertCampaignRules;
using Moe.Modules.EducationAccountTopUp.Application.TopUps.UpsertFixedRecipients;
using Moe.Modules.EducationAccountTopUp.Contracts.TopUps.DTOs;

namespace Moe.StudentFinance.Api.Controllers.EducationAccountTopUp;

[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/top-up-campaigns")]
[ApiController]
[Authorize]
public sealed class TopUpCampaignsController(
    ICommandDispatcher commandDispatcher, 
    IQueryDispatcher queryDispatcher) : ControllerBase
{
    [HttpPost]
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

    [HttpPut("{id:long}")]
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

    [HttpPatch("{id:long}/status")]
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

    [HttpPut("{id:long}/rules")]
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

    [HttpPut("{id:long}/fixed-recipients")]
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

    [HttpGet("{id:long}/preview")]
    [ProducesResponseType(typeof(PreviewCampaignResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Preview(long id, CancellationToken cancellationToken)
    {
        var query = new PreviewCampaignQuery(id);
        var result = await queryDispatcher.Send(query, cancellationToken);

        if (result.IsFailure) return BadRequest(result.Error);
        return Ok(result.Value);
    }

    [HttpPost("{id:long}/execute")]
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

    [HttpGet("{id:long}", Name = "Get")]
    public IActionResult Get(long id) => Ok(new { Id = id });
}
