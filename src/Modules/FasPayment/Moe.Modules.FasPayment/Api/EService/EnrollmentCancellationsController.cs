using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Moe.Application.Abstractions.Messaging;
using Moe.Infrastructure.Shared.Security;
using Moe.Modules.FasPayment.Application.EnrollmentCancellations;
using Moe.Modules.FasPayment.Contracts.Payments;

namespace Moe.Modules.FasPayment.Api.EService;

[ApiController]
[ApiVersion(1.0)]
[Route("api/eservice/v{version:apiVersion}/course-enrollments/{enrollmentId:long}")]
[Authorize(Policy = AuthorizationPolicies.EServicePortal)]
[EnableCors("EServiceCors")]
public sealed class EnrollmentCancellationsController(
    IQueryDispatcher queries,
    ICommandDispatcher commands) : ControllerBase
{
    [HttpGet("cancellation-preview")]
    public async Task<IActionResult> Preview(
        long enrollmentId,
        CancellationToken cancellationToken)
        => this.ToPaymentResponse(await queries.Send(
            new PreviewEnrollmentCancellationQuery(enrollmentId),
            cancellationToken));

    [HttpPost("cancel")]
    public async Task<IActionResult> Cancel(
        long enrollmentId,
        [FromBody] CancelEnrollmentRequest request,
        CancellationToken cancellationToken)
        => this.ToPaymentResponse(await commands.Send(
            new CancelEnrollmentCommand(enrollmentId, request),
            cancellationToken));
}
