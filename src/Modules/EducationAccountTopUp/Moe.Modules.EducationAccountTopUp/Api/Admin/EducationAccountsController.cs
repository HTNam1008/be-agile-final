using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moe.Application.Abstractions.Messaging;
using Moe.Infrastructure.Shared.Api;
using Moe.Infrastructure.Shared.Security;
using Moe.Modules.EducationAccountTopUp.Api;
using Moe.Modules.EducationAccountTopUp.Application.CloseAccount;
using Moe.Modules.EducationAccountTopUp.Application.OpenAccount;
using Moe.Modules.EducationAccountTopUp.Application.TransactionHistory;
using Moe.Modules.EducationAccountTopUp.Application.History.AccountFlatHistory;

namespace Moe.Modules.EducationAccountTopUp.Api.Admin;

[ApiController]
[ApiVersion(1.0)]
[Route("api/admin/v{version:apiVersion}/education-accounts")]
[Authorize(Policy = AuthorizationPolicies.AdminPortal)]
[EnableCors("AdminCors")]
public sealed class EducationAccountsController(
    ICommandDispatcher commands,
    IQueryDispatcher queries) : ControllerBase
{
    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.ManageAccounts)]
    public async Task<IActionResult> OpenManual(
        [FromBody] OpenManualAccountRequest request,
        CancellationToken cancellationToken)
    {
        var command = new OpenManualAccountCommand(request.PersonId, request.ReasonCode, request.Remarks);

        var result = await commands.Send(command, cancellationToken);
        return result.ToCreatedApiResponse(this);
    }

    [HttpPost("{educationAccountId:long}/close")]
    [Authorize(Policy = AuthorizationPolicies.ManageAccountLifecycle)]
    public async Task<IActionResult> CloseManual(
        [FromRoute] long educationAccountId,
        [FromBody] CloseManualAccountRequest request,
        CancellationToken cancellationToken)
    {
        var command = new CloseManualAccountCommand(
            educationAccountId,
            request.ReasonCode,
            request.Remarks);

        var result = await commands.Send(command, cancellationToken);
        return result.IsFailure
            ? TopUpErrorResponseMapper.ToFailureResponse(result.Error, HttpContext)
            : result.ToApiResponse(this, successMessage: "Education Account closed.");
    }

    [HttpGet("{educationAccountId:long}/transactions")]
    [Authorize(Policy = AuthorizationPolicies.ViewAccountDetails)]
    public async Task<IActionResult> GetTransactions(
        [FromRoute] long educationAccountId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var query = new GetAccountTransactionHistoryQuery(
            educationAccountId,
            page,
            pageSize);

        var result = await queries.Send(query, cancellationToken);
        return result.IsFailure
            ? TopUpErrorResponseMapper.ToFailureResponse(result.Error, HttpContext)
            : result.ToApiResponse(this);
    }

    [HttpGet("{educationAccountId:long}/flat-history")]
    [Authorize(Policy = AuthorizationPolicies.ViewAccountDetails)]
    [ProducesResponseType(typeof(ApiResponse<AccountFlatHistoryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetFlatHistory(
        [FromRoute] long educationAccountId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var query = new GetAccountFlatHistoryQuery(
            educationAccountId,
            page,
            pageSize);

        var result = await queries.Send(query, cancellationToken);
        return result.IsFailure
            ? TopUpErrorResponseMapper.ToFailureResponse(result.Error, HttpContext)
            : result.ToApiResponse(this, successMessage: "Account flat history retrieved.");
    }
}
