using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Moe.Application.Abstractions.Messaging;
using Moe.Infrastructure.Shared.Api;
using Moe.Infrastructure.Shared.Security;
using Moe.Modules.IdentityPlatform.Application.Access.AssignAccessScope;
using Moe.Modules.IdentityPlatform.Application.Access.RevokeAccessScope;

namespace Moe.Modules.IdentityPlatform.Api.Admin;

[ApiController]
[ApiVersion(1.0)]
[Route("api/admin/v{version:apiVersion}")]
[Authorize(Policy = AuthorizationPolicies.AdminPortal)]
[EnableCors("AdminCors")]
public sealed class UserAccessScopesController(ICommandDispatcher commands) : ControllerBase
{
    [HttpPost("user-accounts/{userAccountId:long}/access-scopes")]
    [Authorize(Policy = AuthorizationPolicies.ManageAccessScopes)]
    public async Task<IActionResult> Assign(
        long userAccountId,
        [FromBody] AssignAccessScopeRequest request,
        CancellationToken cancellationToken)
    {
        AssignAccessScopeCommand command = new(
            userAccountId,
            request.OrganizationUnitId,
            request.RoleCode,
            request.EffectiveFromUtc);

        var result = await commands.Send(command, cancellationToken);
        return result.ToCreatedApiResponse(this);
    }

    [HttpDelete("user-access-scopes/{scopeId:long}")]
    [Authorize(Policy = AuthorizationPolicies.ManageAccessScopes)]
    public async Task<IActionResult> Revoke(long scopeId, CancellationToken cancellationToken)
    {
        var result = await commands.Send(new RevokeAccessScopeCommand(scopeId), cancellationToken);
        return result.ToApiResponse(this, ApiResponseCodes.NotFound);
    }
}
