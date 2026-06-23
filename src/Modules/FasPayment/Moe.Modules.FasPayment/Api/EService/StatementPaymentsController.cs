using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Moe.Application.Abstractions.Messaging;
using Moe.Infrastructure.Shared.Security;
using Moe.Modules.FasPayment.Application.StatementPayments;
using Moe.Modules.FasPayment.Contracts.Payments;

namespace Moe.Modules.FasPayment.Api.EService;

[ApiController]
[ApiVersion(1.0)]
[Route("api/eservice/v{version:apiVersion}/billing-statements/{statementId:long}")]
[Authorize(Policy = AuthorizationPolicies.EServicePortal)]
[EnableCors("EServiceCors")]
public sealed class StatementPaymentsController(ICommandDispatcher commands, IQueryDispatcher queries) : ControllerBase
{
    [HttpPost("payment-preview")]
    public async Task<IActionResult> Preview(long statementId, CancellationToken ct)
        => this.ToPaymentResponse(await queries.Send(new PreviewStatementPaymentQuery(statementId), ct));

    [HttpPost("payments")]
    public async Task<IActionResult> Pay(long statementId, [FromBody] PayBillingStatementRequest request, CancellationToken ct)
        => this.ToPaymentResponse(await commands.Send(new PayBillingStatementCommand(statementId, request), ct), created: true);

    [HttpPost("defer")]
    public async Task<IActionResult> Defer(long statementId, [FromBody] DeferBillingStatementRequest request, CancellationToken ct)
        => this.ToPaymentResponse(await commands.Send(new DeferBillingStatementCommand(statementId, request), ct));

}
