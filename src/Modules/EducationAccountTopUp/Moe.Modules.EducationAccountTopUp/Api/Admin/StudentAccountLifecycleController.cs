using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Moe.Application.Abstractions.Messaging;
using Moe.Infrastructure.Shared.Api;
using Moe.Infrastructure.Shared.Security;
using Moe.Modules.EducationAccountTopUp.Application.CloseAccount;

namespace Moe.Modules.EducationAccountTopUp.Api.Admin;

[ApiController]
[ApiVersion(1.0)]
[Route("api/admin/v{version:apiVersion}/students")]
[Authorize(Policy = AuthorizationPolicies.AdminPortal)]
[EnableCors("AdminCors")]
public sealed class StudentAccountLifecycleController(ICommandDispatcher commands) : ControllerBase
{
    [HttpPost("{personId:long}/disable")]
    [Authorize(Policy = AuthorizationPolicies.ManageAccountLifecycle)]
    public async Task<IActionResult> Disable(
        [FromRoute] long personId,
        [FromBody] CloseManualAccountRequest request,
        CancellationToken cancellationToken)
    {
        var command = new CloseManualAccountCommand(
            personId,
            request.ReasonCode,
            request.Remarks);

        var result = await commands.Send(command, cancellationToken);
        return result.IsFailure
            ? TopUpErrorResponseMapper.ToFailureResponse(result.Error, HttpContext)
            : result.ToApiResponse(this, successMessage: "Education Account closed.");
    }
}
