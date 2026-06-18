using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Moe.Application.Abstractions.Messaging;
using Moe.Infrastructure.Shared.Api;
using Moe.Infrastructure.Shared.Security;
using Moe.Modules.IdentityPlatform.Application.AdminProfile.GetMyAdminProfile;
using Moe.Modules.IdentityPlatform.Application.AdminProfile.UpdateMyAdminContact;

namespace Moe.Modules.IdentityPlatform.Api.Admin;

[ApiController]
[ApiVersion(1.0)]
[Route("api/admin/v{version:apiVersion}/profile")]
[Authorize(Policy = AuthorizationPolicies.AdminPortal)]
[EnableCors("AdminCors")]
public sealed class AdminProfileController(
    IQueryDispatcher queries,
    ICommandDispatcher commands) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var result = await queries.Send(new GetMyAdminProfileQuery(), cancellationToken);
        return result.ToApiResponse(this, ApiResponseCodes.Unauthorized);
    }

    [HttpPut("contact")]
    public async Task<IActionResult> UpdateContact(
        [FromBody] UpdateMyAdminContactRequest request,
        CancellationToken cancellationToken)
    {
        UpdateMyAdminContactCommand command = new(request.ContactEmail, request.ContactMobile);
        var result = await commands.Send(command, cancellationToken);
        return result.ToApiResponse(this, ApiResponseCodes.Unauthorized);
    }
}
