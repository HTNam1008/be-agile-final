using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Moe.Application.Abstractions.Messaging;
using Moe.Infrastructure.Shared.Security;
using Moe.Modules.FasPayment.Application.Checkout;
using Moe.Modules.FasPayment.Application.PaymentPlans;
using Moe.Modules.FasPayment.Contracts.Payments;

namespace Moe.Modules.FasPayment.Api.EService;

[ApiController]
[ApiVersion(1.0)]
[Route("api/eservice/v{version:apiVersion}/payments")]
[Authorize(Policy = AuthorizationPolicies.EServicePortal)]
[EnableCors("EServiceCors")]
public sealed class StripeCheckoutController(
    ICommandDispatcher commands,
    IQueryDispatcher queries) : ControllerBase
{
    [HttpGet("courses/{courseId:long}/plans")]
    public async Task<IActionResult> ListPlans(long courseId, CancellationToken cancellationToken)
        => this.ToPaymentResponse(await queries.Send(
            new ListCoursePaymentPlansQuery(courseId),
            cancellationToken));

    [HttpPost("checkout-sessions")]
    [EnableRateLimiting("PaymentCheckout")]
    public async Task<IActionResult> CreateCheckout(
        [FromBody] CreateStripeCheckoutRequest request,
        CancellationToken cancellationToken)
        => this.ToPaymentResponse(await commands.Send(
            new CreateStripeCheckoutCommand(request),
            cancellationToken), created: true);

    [HttpGet("checkout-sessions/{checkoutId:long}")]
    public async Task<IActionResult> GetCheckout(long checkoutId, CancellationToken cancellationToken)
        => this.ToPaymentResponse(await queries.Send(
            new GetPaymentCheckoutStatusQuery(checkoutId),
            cancellationToken));
}
