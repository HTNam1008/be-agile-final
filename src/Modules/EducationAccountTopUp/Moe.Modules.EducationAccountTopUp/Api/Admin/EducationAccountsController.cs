using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Moe.Application.Abstractions.Messaging;
using Moe.Infrastructure.Shared.Api;
using Moe.Infrastructure.Shared.Security;
using Moe.Modules.EducationAccountTopUp.Api;
using Moe.Modules.EducationAccountTopUp.Application.CloseAccount;
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

    [HttpPost("{educationAccountId:long}/close")]
    [Authorize(Policy = AuthorizationPolicies.ManageAccountLifecycle)]
    public async Task<IActionResult> CloseManual(
        [FromRoute] long educationAccountId,
        [FromBody] CloseManualAccountRequest request,
        CancellationToken cancellationToken)
    {
        var command = new CloseManualAccountCommand(
            educationAccountId,
            request.ReasonCode,
            request.Remarks);

        var result = await commands.Send(command, cancellationToken);
        return result.IsFailure
            ? TopUpErrorResponseMapper.ToFailureResponse(result.Error, HttpContext)
            : result.ToApiResponse(this, successMessage: "Education Account closed.");
    }
}
