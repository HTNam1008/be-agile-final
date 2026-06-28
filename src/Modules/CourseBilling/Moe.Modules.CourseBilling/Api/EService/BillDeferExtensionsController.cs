using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Moe.Application.Abstractions.Messaging;
using Moe.Infrastructure.Shared.Security;
using Moe.Modules.CourseBilling.Application.DeferExtensions;

namespace Moe.Modules.CourseBilling.Api.EService;

[ApiController]
[ApiVersion(1.0)]
[Route("api/eservice/v{version:apiVersion}/bills")]
[Authorize(Policy = AuthorizationPolicies.EServicePortal)]
[EnableCors("EServiceCors")]
public sealed class BillDeferExtensionsController(ICommandDispatcher commands) : ControllerBase
{
    [HttpPost("{billId:long}/defer-extension-requests")]
    public async Task<IActionResult> Create(long billId, CancellationToken cancellationToken)
        => this.ToCourseBillingResponse(
            await commands.Send(new CreateDeferExtensionRequestCommand(billId), cancellationToken),
            created: true);
}
