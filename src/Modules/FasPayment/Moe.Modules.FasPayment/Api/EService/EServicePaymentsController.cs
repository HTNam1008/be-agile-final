using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moe.Application.Abstractions.Messaging;
using Moe.Infrastructure.Shared.Security;
using Moe.Infrastructure.Shared.Api;
using Moe.Modules.FasPayment.Application.StatementPayments;
using Moe.Modules.FasPayment.Contracts.Payments;

namespace Moe.Modules.FasPayment.Api.EService;

[ApiController]
[ApiVersion(1.0)]
[Route("api/eservice/v{version:apiVersion}/payments")]
[Authorize(Policy = AuthorizationPolicies.EServicePortal)]
[EnableCors("EServiceCors")]
public sealed class EServicePaymentsController(
    IQueryDispatcher queries) : ControllerBase
{
    [HttpGet("history")]
    [ProducesResponseType(typeof(ApiResponse<PageResponse<UserPaymentHistoryResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetHistory(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? status = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] string? sortDirection = null,
        CancellationToken cancellationToken = default)
        => this.ToPaymentResponse(await queries.Send(
            new ListUserPaymentHistoryQuery(page, pageSize, status, sortBy, sortDirection),
            cancellationToken));

}
