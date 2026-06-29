using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Moe.Application.Abstractions.Messaging;
using Moe.Infrastructure.Shared.Security;
using Moe.Modules.CourseBilling.Api;
using Moe.Modules.CourseBilling.Application.AdminStudentCourses;

namespace Moe.Modules.CourseBilling.Api.Admin;

[ApiController]
[ApiVersion(1.0)]
[Route("api/admin/v{version:apiVersion}/students/{personId:long}/courses")]
[Authorize(Policy = AuthorizationPolicies.AdminPortal)]
[EnableCors("AdminCors")]
public sealed class AdminStudentCoursesController(IQueryDispatcher queries) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = AuthorizationPolicies.ViewAccountDetails)]
    public async Task<IActionResult> List(
        [FromRoute] long personId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var query = new GetStudentEnrolledCoursesQuery(personId, page, pageSize);
        return this.ToCourseBillingResponse(await queries.Send(query, cancellationToken));
    }
}
