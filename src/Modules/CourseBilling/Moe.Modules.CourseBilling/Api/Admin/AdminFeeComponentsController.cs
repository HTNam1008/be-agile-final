using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Moe.Application.Abstractions.Messaging;
using Moe.Infrastructure.Shared.Security;
using Moe.Modules.CourseBilling.Api;
using Moe.Modules.CourseBilling.Application.AdminFeeComponents;
using Moe.Modules.CourseBilling.Contracts.AdminFeeComponents;

namespace Moe.Modules.CourseBilling.Api.Admin;

[ApiController]
[ApiVersion(1.0)]
[Route("api/admin/v{version:apiVersion}/fee-components")]
[Authorize(Policy = AuthorizationPolicies.AdminPortal)]
//[Authorize(Policy = AuthorizationPolicies.ManageCourses)]
[EnableCors("AdminCors")]
public sealed class AdminFeeComponentsController(
    ICommandDispatcher commands,
    IQueryDispatcher queries) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] FeeComponentQueryRequest request, CancellationToken cancellationToken)
        => this.ToCourseBillingResponse(await queries.Send(new ListFeeComponentsQuery(request), cancellationToken));

    [HttpGet("{feeComponentId:long}")]
    public async Task<IActionResult> Get(long feeComponentId, CancellationToken cancellationToken)
        => this.ToCourseBillingResponse(await queries.Send(new GetFeeComponentQuery(feeComponentId), cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateFeeComponentRequest request, CancellationToken cancellationToken)
        => this.ToCourseBillingResponse(
            await commands.Send(new CreateFeeComponentCommand(request), cancellationToken),
            created: true);

    [HttpPut("{feeComponentId:long}")]
    public async Task<IActionResult> Update(
        long feeComponentId,
        [FromBody] UpdateFeeComponentRequest request,
        CancellationToken cancellationToken)
        => this.ToCourseBillingResponse(
            await commands.Send(new UpdateFeeComponentCommand(feeComponentId, request), cancellationToken));

    [HttpPost("{feeComponentId:long}/activate")]
    public async Task<IActionResult> Activate(long feeComponentId, CancellationToken cancellationToken)
        => this.ToCourseBillingResponse(
            await commands.Send(new ActivateFeeComponentCommand(feeComponentId), cancellationToken));

    [HttpPost("{feeComponentId:long}/deactivate")]
    public async Task<IActionResult> Deactivate(long feeComponentId, CancellationToken cancellationToken)
        => this.ToCourseBillingResponse(
            await commands.Send(new DeactivateFeeComponentCommand(feeComponentId), cancellationToken));
}
