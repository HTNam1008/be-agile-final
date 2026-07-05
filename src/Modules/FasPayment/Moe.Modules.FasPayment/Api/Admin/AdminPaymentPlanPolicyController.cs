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
[Route("api/admin/v{version:apiVersion}/course-payment-plan-policy")]
[Authorize(Policy = AuthorizationPolicies.AdminPortal)]
[EnableCors("AdminCors")]
public sealed class AdminPaymentPlanPolicyController(
    IQueryDispatcher queries) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
        => this.ToPaymentResponse(await queries.Send(new GetCoursePaymentPlanPolicyQuery(), cancellationToken));
}
