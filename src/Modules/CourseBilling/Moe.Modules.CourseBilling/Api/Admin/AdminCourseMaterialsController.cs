using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Moe.Application.Abstractions.Messaging;
using Moe.Infrastructure.Shared.Security;
using Moe.Modules.CourseBilling.Api;
using Moe.Modules.CourseBilling.Application.AdminCourses.Materials;
using Moe.Modules.CourseBilling.Contracts.AdminCourses;

namespace Moe.Modules.CourseBilling.Api.Admin;

[ApiController]
[ApiVersion(1.0)]
[Route("api/admin/v{version:apiVersion}/courses/{courseId:long}/materials")]
[Authorize(Policy = AuthorizationPolicies.AdminPortal)]
//[Authorize(Policy = AuthorizationPolicies.ManageCourses)]
[EnableCors("AdminCors")]
public sealed class AdminCourseMaterialsController(
    ICommandDispatcher commands,
    IQueryDispatcher queries) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(long courseId, CancellationToken cancellationToken)
        => this.ToCourseBillingResponse(await queries.Send(new ListCourseMaterialsQuery(courseId), cancellationToken));

    [HttpPost]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Add(
        long courseId,
        [FromForm] CreateCourseMaterialRequest request,
        CancellationToken cancellationToken)
        => this.ToCourseBillingResponse(await commands.Send(new AddCourseMaterialCommand(courseId, request), cancellationToken), created: true);

    [HttpPut("{courseMaterialId:long}")]
    public async Task<IActionResult> Update(
        long courseId,
        long courseMaterialId,
        [FromBody] UpdateCourseMaterialRequest request,
        CancellationToken cancellationToken)
        => this.ToCourseBillingResponse(await commands.Send(new UpdateCourseMaterialCommand(courseId, courseMaterialId, request), cancellationToken));

    [HttpPost("{courseMaterialId:long}/replace-file")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> ReplaceFile(
        long courseId,
        long courseMaterialId,
        [FromForm] ReplaceCourseMaterialFileRequest request,
        CancellationToken cancellationToken)
        => this.ToCourseBillingResponse(await commands.Send(new ReplaceCourseMaterialFileCommand(courseId, courseMaterialId, request), cancellationToken));

    [HttpDelete("{courseMaterialId:long}")]
    public async Task<IActionResult> Delete(long courseId, long courseMaterialId, CancellationToken cancellationToken)
        => this.ToCourseBillingResponse(await commands.Send(new DeleteCourseMaterialCommand(courseId, courseMaterialId), cancellationToken));
}
