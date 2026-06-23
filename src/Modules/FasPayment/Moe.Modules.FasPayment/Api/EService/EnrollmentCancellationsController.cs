using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Moe.Application.Abstractions.Messaging;
using Moe.Infrastructure.Shared.Security;
using Moe.Modules.FasPayment.Application.EnrollmentCancellations;

namespace Moe.Modules.FasPayment.Api.EService;

[ApiController]
[ApiVersion(1.0)]
[Route("api/eservice/v{version:apiVersion}/course-enrollments/{enrollmentId:long}/cancellation-preview")]
[Authorize(Policy = AuthorizationPolicies.EServicePortal)]
[EnableCors("EServiceCors")]
public sealed class EnrollmentCancellationsController(IQueryDispatcher queries) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Preview(
        long enrollmentId,
        CancellationToken cancellationToken)
        => this.ToPaymentResponse(await queries.Send(
            new PreviewEnrollmentCancellationQuery(enrollmentId),
            cancellationToken));
}
