using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Moe.Application.Abstractions.Messaging;
using Moe.Infrastructure.Shared.Api;
using Moe.Infrastructure.Shared.Security;
using Moe.Modules.CourseBilling.Application.Enrollments.AdminEnrollPerson;

namespace Moe.Modules.CourseBilling.Api.Admin;

[ApiController]
[ApiVersion(1.0)]
[Route("api/admin/v{version:apiVersion}/courses")]
[Authorize(Policy = AuthorizationPolicies.AdminPortal)]
[EnableCors("AdminCors")]
public sealed class CourseEnrollmentsController(ICommandDispatcher commands) : ControllerBase
{
    [HttpPost("{courseId:long}/enrollments")]
    [AllowAnonymous]
    public async Task<IActionResult> EnrollPerson(
        long courseId,
        [FromBody] AdminEnrollPersonRequest request,
        CancellationToken cancellationToken)
    {
        var command = new AdminEnrollPersonCommand(courseId, request.PersonId);

        var result = await commands.Send(command, cancellationToken);
        return result.ToCreatedApiResponse(this);
    }
}
