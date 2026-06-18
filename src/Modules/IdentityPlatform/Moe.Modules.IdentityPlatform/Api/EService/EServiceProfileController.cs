using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Moe.Application.Abstractions.Messaging;
using Moe.Infrastructure.Shared.Api;
using Moe.Infrastructure.Shared.Security;
using Moe.Modules.IdentityPlatform.Application.StudentProfile.GetMyStudentProfile;
using Moe.Modules.IdentityPlatform.Application.StudentProfile.UpdateContactPreferences;
using Moe.Modules.IdentityPlatform.Application.StudentProfile.UpdateMyStudentContact;

namespace Moe.Modules.IdentityPlatform.Api.EService;

[ApiController]
[ApiVersion(1.0)]
[Route("api/eservice/v{version:apiVersion}/profile")]
[Authorize(Policy = AuthorizationPolicies.EServicePortal)]
[EnableCors("EServiceCors")]
public sealed class EServiceProfileController(
    IQueryDispatcher queries,
    ICommandDispatcher commands) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var result = await queries.Send(new GetMyStudentProfileQuery(), cancellationToken);
        return result.ToApiResponse(this, ApiResponseCodes.Unauthorized);
    }

    [HttpGet("~/api/v{version:apiVersion}/me/profile")]
    public async Task<IActionResult> GetCanonical(CancellationToken cancellationToken)
    {
        var result = await queries.Send(new GetMyStudentProfileQuery(), cancellationToken);
        return result.ToApiResponse(this, ApiResponseCodes.Unauthorized);
    }

    [HttpPut("contact")]
    public async Task<IActionResult> UpdateContact(
        [FromBody] UpdateMyStudentContactRequest request,
        CancellationToken cancellationToken)
    {
        UpdateMyStudentContactCommand command = new(request.ContactEmail, request.ContactMobile);
        var result = await commands.Send(command, cancellationToken);
        return result.ToApiResponse(this, ApiResponseCodes.Unauthorized);
    }

    [HttpPatch("~/api/v{version:apiVersion}/me/contact-preferences")]
    public async Task<IActionResult> UpdateContactPreferences(
        [FromBody] UpdateContactPreferencesRequest request,
        CancellationToken cancellationToken)
    {
        UpdateContactPreferencesCommand command = new(
            request.PreferredEmail,
            request.PreferredMobile,
            request.PreferredAddress,
            request.ExpectedUpdatedAtUtc);

        var result = await commands.Send(command, cancellationToken);
        return result.ToApiResponse(this, ApiResponseCodes.Conflict);
    }
}
