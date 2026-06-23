using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Moe.Application.Abstractions.Messaging;
using Moe.Infrastructure.Shared.Security;
using Moe.Modules.FasPayment.Application.AdminPayments;
using Moe.Modules.FasPayment.Contracts.Payments;

namespace Moe.Modules.FasPayment.Api.Admin;

[ApiController]
[ApiVersion(1.0)]
[Route("api/admin/v{version:apiVersion}/payments")]
[Authorize(Policy = AuthorizationPolicies.AdminPortal)]
[EnableCors("AdminCors")]
public sealed class AdminPaymentsController(ICommandDispatcher commands, IQueryDispatcher queries) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
        => this.ToPaymentResponse(await queries.Send(new ListAdminPaymentsQuery(), cancellationToken));

    [HttpGet("webhook-events")]
    public async Task<IActionResult> ListWebhookEvents(CancellationToken cancellationToken)
        => this.ToPaymentResponse(await queries.Send(new ListPaymentWebhookEventsQuery(), cancellationToken));

    [HttpPost("{paymentId:long}/refunds")]
    [EnableRateLimiting("PaymentCheckout")]
    public async Task<IActionResult> Refund(
        long paymentId,
        [FromBody] CreatePaymentRefundRequest request,
        CancellationToken cancellationToken)
        => this.ToPaymentResponse(await commands.Send(
            new CreatePaymentRefundCommand(paymentId, request),
            cancellationToken), created: true);
}
