using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moe.Application.Abstractions.Messaging;
using Moe.Infrastructure.Shared.Api;
using Moe.Infrastructure.Shared.Security;
using Moe.Modules.EducationAccountTopUp.Application.History;
using Moe.Modules.EducationAccountTopUp.Application.History.AccountTopUpTransactionHistory;
using Moe.Modules.EducationAccountTopUp.Application.History.AllTransactionsHistory;
using Moe.Modules.EducationAccountTopUp.Application.History.CampaignHistory;
using Moe.Modules.EducationAccountTopUp.Application.History.CampaignTransactionHistory;
using Moe.Modules.EducationAccountTopUp.Application.History.RunHistory;
using Moe.SharedKernel.Results;

namespace Moe.Modules.EducationAccountTopUp.Api.Admin;

[ApiController]
[ApiVersion(1.0)]
[Route("api/admin/v{version:apiVersion}/top-up-history")]
[Authorize(Policy = AuthorizationPolicies.AdminPortal)]
[EnableCors("AdminCors")]
public sealed class TopUpHistoryController(IQueryDispatcher queries) : ControllerBase
{
    /// <summary>
    /// Returns the current campaign history view with server-side filters and pagination.
    /// </summary>
    [HttpGet("campaigns")]
    [Authorize(Policy = AuthorizationPolicies.ViewTopUps)]
    [ProducesResponseType(
        typeof(ApiResponse<PageResponse<CampaignHistoryItem>>),
        StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetCampaignHistory(
        [FromQuery] CampaignHistoryRequest request,
        CancellationToken cancellationToken)
    {
        TopUpHistoryFilter filter = new(
            request.DateFromUtc,
            request.DateToUtc,
            request.CampaignId,
            request.CampaignSearch,
            request.OrganizationId,
            TriggerType: null,
            request.Status,
            StudentOrAccountSearch: null,
            request.ActorId);

        Result<PageResponse<CampaignHistoryItem>> result = await queries.Send(
            new GetCampaignHistoryQuery(filter, request.Page, request.PageSize),
            cancellationToken);

        return ToHistoryResponse(result, "Campaign history retrieved.");
    }

    /// <summary>
    /// Returns run history with server-side filters and deterministic pagination.
    /// </summary>
    [HttpGet("runs")]
    [Authorize(Policy = AuthorizationPolicies.ViewTopUps)]
    [ProducesResponseType(
        typeof(ApiResponse<PageResponse<RunHistoryItem>>),
        StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetRunHistory(
        [FromQuery] RunHistoryRequest request,
        CancellationToken cancellationToken)
    {
        TopUpHistoryFilter filter = new(
            request.DateFromUtc,
            request.DateToUtc,
            request.CampaignId,
            request.CampaignSearch,
            request.OrganizationId,
            request.TriggerType,
            request.Status,
            request.StudentOrAccountSearch,
            request.ActorId);

        Result<PageResponse<RunHistoryItem>> result = await queries.Send(
            new GetRunHistoryQuery(filter, request.Page, request.PageSize),
            cancellationToken);

        return ToHistoryResponse(result, "Run history retrieved.");
    }

    [HttpGet("campaigns/{campaignId:long}/transactions")]
    [Authorize(Policy = AuthorizationPolicies.ViewTopUps)]
    [ProducesResponseType(typeof(ApiResponse<PageResponse<CampaignTransactionHistoryItem>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetCampaignTransactions(
        long campaignId,
        [FromQuery] CampaignTransactionHistoryRequest request,
        CancellationToken cancellationToken)
    {
        TopUpHistoryFilter filter = new(
            request.DateFromUtc, request.DateToUtc,
            CampaignId: campaignId, CampaignSearch: null,
            request.OrganizationId, TriggerType: null,
            request.Status, StudentOrAccountSearch: null,
            ActorId: null);

        Result<PageResponse<CampaignTransactionHistoryItem>> result = await queries.Send(
            new GetCampaignTransactionHistoryQuery(campaignId, filter, request.Page, request.PageSize),
            cancellationToken);

        return ToHistoryResponse(result, "Campaign transaction history retrieved.");
    }

    [HttpGet("accounts/{accountId:long}/transactions")]
    [Authorize(Policy = AuthorizationPolicies.ViewTopUps)]
    [ProducesResponseType(typeof(ApiResponse<PageResponse<AccountTopUpTransactionHistoryItem>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAccountTransactions(
        long accountId,
        [FromQuery] AccountTransactionHistoryRequest request,
        CancellationToken cancellationToken)
    {
        TopUpHistoryFilter filter = new(
            request.DateFromUtc, request.DateToUtc,
            CampaignId: null, CampaignSearch: null,
            OrganizationId: null, TriggerType: null,
            request.Status, StudentOrAccountSearch: null,
            ActorId: null);

        Result<PageResponse<AccountTopUpTransactionHistoryItem>> result = await queries.Send(
            new GetAccountTopUpTransactionHistoryQuery(accountId, filter, request.Page, request.PageSize),
            cancellationToken);

        return ToHistoryResponse(result, "Account transaction history retrieved.");
    }

    [HttpGet("transactions")]
    [Authorize(Policy = AuthorizationPolicies.ViewTopUps)]
    [ProducesResponseType(typeof(ApiResponse<PageResponse<AllTransactionsItem>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAllTransactions(
        [FromQuery] AllTransactionsRequest request,
        CancellationToken cancellationToken)
    {
        TopUpHistoryFilter filter = new(
            request.DateFromUtc, request.DateToUtc,
            CampaignId: null, request.CampaignSearch,
            request.OrganizationId, TriggerType: null,
            request.Status, StudentOrAccountSearch: null,
            ActorId: null);

        Result<PageResponse<AllTransactionsItem>> result = await queries.Send(
            new GetAllTransactionsQuery(filter, request.Page, request.PageSize),
            cancellationToken);

        return ToHistoryResponse(result, "Transactions retrieved.");
    }

    private IActionResult ToHistoryResponse<T>(Result<T> result, string message)
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
