using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Moe.Application.Abstractions.Messaging;
using Moe.Infrastructure.Shared.Api;
using Moe.Infrastructure.Shared.Security;
using Moe.Modules.FasPayment.Application.StatementPayments;
using Moe.Modules.FasPayment.Contracts.Payments;
using Moe.Modules.FasPayment.Domain.Payments;

namespace Moe.Modules.FasPayment.Api.EService;

[ApiController]
[ApiVersion(1.0)]
[Route("api/eservice/v{version:apiVersion}/billing-statements/{statementId:long}")]
[Authorize(Policy = AuthorizationPolicies.EServicePortal)]
[EnableCors("EServiceCors")]
public sealed class StatementPaymentsController(ICommandDispatcher commands, IQueryDispatcher queries) : ControllerBase
{
    [HttpPost("payment-preview")]
    public async Task<IActionResult> Preview(
        long statementId,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] PreviewStatementPaymentRequest? request,
        CancellationToken ct)
        => this.ToPaymentResponse(await queries.Send(
            new PreviewStatementPaymentQuery(statementId, request?.BillIds),
            ct));

    [HttpPost("payments")]
    public async Task<IActionResult> Pay(long statementId, [FromBody] PayBillingStatementRequest request, CancellationToken ct)
        => this.ToPaymentResponse(await commands.Send(new PayBillingStatementCommand(statementId, request), ct), created: true);

    [HttpPost("payments/{paymentId:long}/cancel")]
    public async Task<IActionResult> Cancel(long statementId, long paymentId, CancellationToken ct)
        => this.ToPaymentResponse(await commands.Send(
            new CancelBillingStatementPaymentCommand(statementId, paymentId),
            ct));

    [HttpPost("defer")]
    public async Task<IActionResult> Defer(long statementId, [FromBody] DeferBillingStatementRequest request, CancellationToken ct)
    {
        var result = await commands.Send(new DeferBillingStatementCommand(statementId, request), ct);
        if (result.IsFailure)
        {
            return this.ToPaymentResponse(result);
        }

        DeferBillingStatementResponse response = result.Value;
        if (!response.Deferred &&
            response.BlockedReasonCode == PaymentDomainErrors.EducationAccountCanCoverDeferral.Code)
        {
            return new ObjectResult(ApiResponse<DeferBillingStatementResponse>.Fail(
                PaymentDomainErrors.EducationAccountCanCoverDeferral.Message,
                [PaymentDomainErrors.EducationAccountCanCoverDeferral.Code],
                ApiResponseCodes.BadRequest,
                HttpContext.TraceIdentifier,
                response))
            {
                StatusCode = ApiResponseCodes.BadRequest
            };
        }

        return ApiResponseFactory.Ok(response, HttpContext.TraceIdentifier);
    }

}
