using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Moe.Application.Abstractions.Messaging;
using Moe.Infrastructure.Shared.Api;
using Moe.Infrastructure.Shared.Security;
using Moe.Modules.EducationAccountTopUp.Api;
using Moe.Modules.EducationAccountTopUp.Application.CloseAccount;
using Moe.Modules.EducationAccountTopUp.Application.OpenAccount;
using Moe.Modules.EducationAccountTopUp.Application.TransactionHistory;

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
    // Internal fallback only. FE manual student creation must use POST /students, which creates the account atomically.
    [Authorize(Policy = AuthorizationPolicies.InternalAccountProvisioning)]
    public async Task<IActionResult> OpenManual(
        [FromBody] OpenManualAccountRequest request,
        CancellationToken cancellationToken)
    {
        var command = new OpenManualAccountCommand(request.PersonId, request.ReasonCode, request.Remarks);

        var result = await commands.Send(command, cancellationToken);
        return result.ToCreatedApiResponse(this);
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
}
