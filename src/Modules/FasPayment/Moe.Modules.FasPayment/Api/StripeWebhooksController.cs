using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Moe.Application.Abstractions.Messaging;
using Moe.Modules.FasPayment.Application.Webhooks;

namespace Moe.Modules.FasPayment.Api;

[ApiController]
[Route("api/webhooks/stripe")]
[AllowAnonymous]
public sealed class StripeWebhooksController(ICommandDispatcher commands) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Process(CancellationToken cancellationToken)
    {
        using StreamReader reader = new(Request.Body);
        string payload = await reader.ReadToEndAsync(cancellationToken);
        string signature = Request.Headers["Stripe-Signature"].ToString();
        return this.ToPaymentResponse(await commands.Send(
            new ProcessStripeWebhookCommand(payload, signature),
            cancellationToken));
    }
}
