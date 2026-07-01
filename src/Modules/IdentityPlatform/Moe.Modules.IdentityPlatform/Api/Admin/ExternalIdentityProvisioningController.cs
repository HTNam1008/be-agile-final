using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Moe.Application.Abstractions.Messaging;
using Moe.Infrastructure.Shared.Api;
using Moe.Infrastructure.Shared.Security;
using Moe.Modules.IdentityPlatform.Application.ExternalProvisioning;
using Moe.Modules.IdentityPlatform.Application.ExternalProvisioning.DisableUserAccount;
using Moe.Modules.IdentityPlatform.Application.ExternalProvisioning.EnableUserAccount;
using Moe.Modules.IdentityPlatform.Application.ExternalProvisioning.ProvisionStudentSingpassAccount;
using Moe.Modules.IdentityPlatform.Application.ExternalProvisioning.RetryIdentityProvisioning;

namespace Moe.Modules.IdentityPlatform.Api.Admin;

[ApiController]
[ApiVersion(1.0)]
[Route("api/admin/v{version:apiVersion}")]
[Authorize(Policy = AuthorizationPolicies.AdminPortal)]
[EnableCors("AdminCors")]
public sealed class ExternalIdentityProvisioningController(
    ICommandDispatcher commands,
    IQueryDispatcher queries) : ControllerBase
{
    [HttpPost("people/{personId:long}/external-identities/singpass")]
    [Authorize(Policy = AuthorizationPolicies.ManageExternalAccounts)]
    public async Task<IActionResult> ProvisionSingpass(
        long personId,
        [FromBody] ProvisionStudentSingpassAccountRequest request,
        CancellationToken cancellationToken)
    {
        ProvisionStudentSingpassAccountCommand command = new(
            personId,
            request.ExternalIssuer,
            request.SingpassSubjectId,
            request.DisplayName,
            request.IdempotencyKey);

        var result = await commands.Send(command, cancellationToken);
        return result.ToCreatedApiResponse(this);
    }

    [HttpGet("identity-provisioning-requests/{requestId:long}")]
    [Authorize(Policy = AuthorizationPolicies.ManageExternalAccounts)]
    public async Task<IActionResult> GetProvisioningRequest(
        long requestId,
        CancellationToken cancellationToken)
    {
        var result = await queries.Send(new GetIdentityProvisioningRequestQuery(requestId), cancellationToken);
        return result.ToApiResponse(this, ApiResponseCodes.NotFound);
    }

    [HttpPost("identity-provisioning-requests/{requestId:long}/retry")]
    [Authorize(Policy = AuthorizationPolicies.ManageExternalAccounts)]
    public async Task<IActionResult> Retry(long requestId, CancellationToken cancellationToken)
    {
        var result = await commands.Send(new RetryIdentityProvisioningCommand(requestId), cancellationToken);
        return result.ToApiResponse(this, ApiResponseCodes.NotFound);
    }

    [HttpPost("user-accounts/{userAccountId:long}/disable")]
    [Authorize(Policy = AuthorizationPolicies.ManageExternalAccounts)]
    public async Task<IActionResult> DisableAccount(long userAccountId, CancellationToken cancellationToken)
    {
        var result = await commands.Send(new DisableUserAccountCommand(userAccountId), cancellationToken);
        return result.ToApiResponse(this, ApiResponseCodes.NotFound);
    }

    [HttpPost("user-accounts/{userAccountId:long}/enable")]
    [Authorize(Policy = AuthorizationPolicies.ManageExternalAccounts)]
    public async Task<IActionResult> EnableAccount(long userAccountId, CancellationToken cancellationToken)
    {
        var result = await commands.Send(new EnableUserAccountCommand(userAccountId), cancellationToken);
        return result.ToApiResponse(this, ApiResponseCodes.NotFound);
    }
}
