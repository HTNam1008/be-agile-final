using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moe.Application.Abstractions.Messaging;
using Moe.Infrastructure.Shared.Api;
using Moe.Infrastructure.Shared.Security;
using Moe.Modules.EducationAccountTopUp.Application.History.ContractStatus;
using Moe.SharedKernel.Results;

namespace Moe.Modules.EducationAccountTopUp.Api.Admin;

[ApiController]
[ApiVersion(1.0)]
[Route("api/admin/v{version:apiVersion}/top-up-campaigns/{campaignId:long}/contracts")]
[Authorize(Policy = AuthorizationPolicies.AdminPortal)]
[EnableCors("AdminCors")]
public sealed class TopUpContractController(IQueryDispatcher queries) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = AuthorizationPolicies.ViewTopUps)]
    [ProducesResponseType(typeof(ApiResponse<PageResponse<CampaignContractItem>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetContracts(
        long campaignId,
        [FromQuery] CampaignContractsRequest request,
        CancellationToken cancellationToken)
    {
        Result<PageResponse<CampaignContractItem>> result = await queries.Send(
            new GetCampaignContractsQuery(
                campaignId,
                request.ContractStatus,
                request.AccountId,
                request.Page,
                request.PageSize),
            cancellationToken);

        if (result.IsFailure)
        {
            return TopUpErrorResponseMapper.ToFailureResponse(result.Error, HttpContext);
        }

        return ApiResponseFactory.Ok(
            result.Value,
            HttpContext.TraceIdentifier,
            "Campaign contracts retrieved.");
    }
}

public sealed class CampaignContractsRequest
{
    public string? ContractStatus { get; init; }
    public long? AccountId { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 25;
}
