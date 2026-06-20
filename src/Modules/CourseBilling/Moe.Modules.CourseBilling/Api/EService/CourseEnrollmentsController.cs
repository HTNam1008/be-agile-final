using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Moe.Application.Abstractions.Messaging;
using Moe.Infrastructure.Shared.Security;
using Moe.Modules.CourseBilling.Api;
using Moe.Modules.CourseBilling.Application.Enrollments.SelfJoinCourse;
using Moe.Modules.CourseBilling.Contracts.Enrollments;

namespace Moe.Modules.CourseBilling.Api.EService;

[ApiController]
[ApiVersion(1.0)]
[Route("api/eservice/v{version:apiVersion}/course-enrollments")]
[Authorize(Policy = AuthorizationPolicies.EServicePortal)]
[EnableCors("EServiceCors")]
public sealed class CourseEnrollmentsController(ICommandDispatcher commands) : ControllerBase
{
    [HttpPost]
    [AllowAnonymous]
    public async Task<IActionResult> JoinCourse(
        [FromBody] SelfJoinCourseRequest request,
        CancellationToken cancellationToken)
    {
        var result = await commands.Send(new SelfJoinCourseCommand(request.CourseId), cancellationToken);
        return this.ToCourseBillingResponse(result, created: true);
    }
}
