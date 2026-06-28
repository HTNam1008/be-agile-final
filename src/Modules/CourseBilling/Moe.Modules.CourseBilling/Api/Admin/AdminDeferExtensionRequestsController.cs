using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Moe.Application.Abstractions.Messaging;
using Moe.Infrastructure.Shared.Security;
using Moe.Modules.CourseBilling.Application.DeferExtensions;
using Moe.Modules.CourseBilling.Contracts.DeferExtensions;

namespace Moe.Modules.CourseBilling.Api.Admin;

[ApiController]
[ApiVersion(1.0)]
[Route("api/admin/v{version:apiVersion}/defer-extension-requests")]
[Authorize(Policy = AuthorizationPolicies.AdminPortal)]
[EnableCors("AdminCors")]
public sealed class AdminDeferExtensionRequestsController(
    IQueryDispatcher queries,
    ICommandDispatcher commands) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] DeferExtensionRequestQueryRequest request,
        CancellationToken cancellationToken)
        => this.ToCourseBillingResponse(
            await queries.Send(new ListDeferExtensionRequestsQuery(request), cancellationToken));

    [HttpPost("{requestId:long}/approve")]
    public async Task<IActionResult> Approve(long requestId, CancellationToken cancellationToken)
        => this.ToCourseBillingResponse(
            await commands.Send(new ApproveDeferExtensionRequestCommand(requestId), cancellationToken));

    [HttpPost("{requestId:long}/reject")]
    public async Task<IActionResult> Reject(long requestId, CancellationToken cancellationToken)
        => this.ToCourseBillingResponse(
            await commands.Send(new RejectDeferExtensionRequestCommand(requestId), cancellationToken));
}
