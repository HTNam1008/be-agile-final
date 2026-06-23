using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Moe.Application.Abstractions.Messaging;
using Moe.Infrastructure.Shared.Api;
using Moe.Infrastructure.Shared.Security;
using Moe.Modules.EducationAccountTopUp.Application.OpenAccount;

namespace Moe.Modules.EducationAccountTopUp.Api.Admin;

[ApiController]
[ApiVersion(1.0)]
[Route("api/admin/v{version:apiVersion}/education-accounts")]
[Authorize(Policy = AuthorizationPolicies.AdminPortal)]
[EnableCors("AdminCors")]
public sealed class EducationAccountsController(ICommandDispatcher commands) : ControllerBase
{
    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.ManageAccounts)]
    public async Task<IActionResult> OpenManual(
        [FromBody] OpenManualAccountRequest request,
        CancellationToken cancellationToken)
    {
        var command = new OpenManualAccountCommand(request.PersonId, request.ReasonCode, request.Remarks);

        var result = await commands.Send(command, cancellationToken);
        return result.ToCreatedApiResponse(this);
    }
}
