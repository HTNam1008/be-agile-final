using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Moe.Application.Abstractions.Messaging;
using Moe.Infrastructure.Shared.Api;
using Moe.Infrastructure.Shared.Security;
using Moe.Modules.CourseBilling.Api;
using Moe.Modules.CourseBilling.Application.BillingConfiguration;
using Moe.Modules.CourseBilling.Contracts.BillingConfiguration;

namespace Moe.Modules.CourseBilling.Api.Admin;

[ApiController]
[ApiVersion(1.0)]
[Route("api/admin/v{version:apiVersion}/billing-configuration")]
[Authorize(Policy = AuthorizationPolicies.AdminPortal)]
[EnableCors("AdminCors")]
public sealed class AdminBillingConfigurationController(
    IQueryDispatcher queries,
    ICommandDispatcher commands) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] long? organizationId, CancellationToken cancellationToken)
        => this.ToCourseBillingResponse(
            await queries.Send(new GetBillingConfigurationQuery(organizationId), cancellationToken));

    [HttpPut]
    public async Task<IActionResult> Update(
        [FromBody] UpdateBillingConfigurationRequest request,
        CancellationToken cancellationToken)
        => this.ToCourseBillingResponse(
            await commands.Send(new UpdateBillingConfigurationCommand(request), cancellationToken));
}
