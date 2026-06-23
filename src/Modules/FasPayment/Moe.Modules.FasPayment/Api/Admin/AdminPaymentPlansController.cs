using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Moe.Application.Abstractions.Messaging;
using Moe.Infrastructure.Shared.Security;
using Moe.Modules.FasPayment.Application.PaymentPlans;
using Moe.Modules.FasPayment.Contracts.Payments;

namespace Moe.Modules.FasPayment.Api.Admin;

[ApiController]
[ApiVersion(1.0)]
[Route("api/admin/v{version:apiVersion}/courses/{courseId:long}/payment-plans")]
[Authorize(Policy = AuthorizationPolicies.AdminPortal)]
[EnableCors("AdminCors")]
public sealed class AdminPaymentPlansController(
    ICommandDispatcher commands,
    IQueryDispatcher queries) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(long courseId, CancellationToken cancellationToken)
        => this.ToPaymentResponse(await queries.Send(new ListCoursePaymentPlansQuery(courseId), cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Create(
        long courseId,
        [FromBody] CreateCoursePaymentPlanRequest request,
        CancellationToken cancellationToken)
        => this.ToPaymentResponse(await commands.Send(
            new CreateCoursePaymentPlanCommand(courseId, request),
            cancellationToken), created: true);
}
