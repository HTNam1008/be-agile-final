using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Moe.Infrastructure.Shared.Security;
using Moe.Modules.FasPayment.Contracts.Payments;
using Moe.Application.Abstractions.Messaging;
using Moe.Modules.FasPayment.Application.LegacyPayments;
using Moe.Modules.FasPayment.Application.StatementPayments;

namespace Moe.Modules.FasPayment.Api.EService;

[ApiController]
[ApiVersion(1.0)]
[Route("api/eservice/v{version:apiVersion}/payments")]
[Authorize(Policy = AuthorizationPolicies.EServicePortal)]
[EnableCors("EServiceCors")]
public sealed class EServicePaymentsController(
    ICommandDispatcher commands,
    IQueryDispatcher queries) : ControllerBase
{
    [HttpGet("history")]
    public async Task<IActionResult> GetHistory(CancellationToken cancellationToken)
        => this.ToPaymentResponse(await queries.Send(
            new ListUserPaymentHistoryQuery(),
            cancellationToken));

    [HttpGet("outstanding-bills")]
    public async Task<IActionResult> GetOutstandingBills(CancellationToken cancellationToken)
    {
        return this.ToPaymentResponse(await queries.Send(
            new GetOutstandingBillsQuery(),
            cancellationToken));
    }

    [HttpPost("pay")]
    public async Task<IActionResult> PayBill(
        [FromBody] PayBillRequest request,
        CancellationToken cancellationToken)
    {
        return this.ToPaymentResponse(await commands.Send(
            new PayOutstandingBillCommand(request),
            cancellationToken), created: true);
    }
}
