using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Moe.Application.Abstractions.Messaging;
using Moe.Infrastructure.Shared.Security;
using Moe.Modules.CourseBilling.Api;
using Moe.Modules.CourseBilling.Application.AdminCourses.Courses;
using Moe.Modules.CourseBilling.Application.AdminCourses.Enrollments;
using Moe.Modules.CourseBilling.Application.AdminCourses.Fees;
using Moe.Modules.CourseBilling.Contracts.AdminCourses;

namespace Moe.Modules.CourseBilling.Api.Admin;

[ApiController]
[ApiVersion(1.0)]
[Route("api/admin/v{version:apiVersion}/courses")]
[Authorize(Policy = AuthorizationPolicies.AdminPortal)]
[EnableCors("AdminCors")]
public sealed class AdminCoursesController(
    ICommandDispatcher commands,
    IQueryDispatcher queries) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] CourseQueryRequest request, CancellationToken cancellationToken)
        => this.ToCourseBillingResponse(await queries.Send(new ListCoursesQuery(request), cancellationToken));

    [HttpGet("{courseId:long}")]
    public async Task<IActionResult> Get(long courseId, CancellationToken cancellationToken)
        => this.ToCourseBillingResponse(await queries.Send(new GetCourseQuery(courseId), cancellationToken));

    [HttpGet("{courseId:long}/preview")]
    public async Task<IActionResult> Preview(long courseId, CancellationToken cancellationToken)
        => this.ToCourseBillingResponse(await queries.Send(new PreviewCourseQuery(courseId), cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateCourseRequest request, CancellationToken cancellationToken)
        => this.ToCourseBillingResponse(await commands.Send(new CreateCourseCommand(request), cancellationToken), created: true);

    [HttpPut("{courseId:long}")]
    public async Task<IActionResult> Update(long courseId, [FromBody] UpdateCourseRequest request, CancellationToken cancellationToken)
        => this.ToCourseBillingResponse(await commands.Send(new UpdateCourseCommand(courseId, request), cancellationToken));

    [HttpDelete("{courseId:long}")]
    public async Task<IActionResult> Remove(long courseId, CancellationToken cancellationToken)
        => this.ToCourseBillingResponse(await commands.Send(new RemoveCourseCommand(courseId), cancellationToken));

    [HttpPost("{courseId:long}/publish")]
    public async Task<IActionResult> Publish(long courseId, CancellationToken cancellationToken)
        => this.ToCourseBillingResponse(await commands.Send(new PublishCourseCommand(courseId), cancellationToken));

    [HttpPost("{courseId:long}/disable")]
    public async Task<IActionResult> Disable(long courseId, [FromBody] DisableCourseRequest request, CancellationToken cancellationToken)
        => this.ToCourseBillingResponse(await commands.Send(new DisableCourseCommand(courseId, request), cancellationToken));

    [HttpPost("{courseId:long}/enable")]
    public async Task<IActionResult> Enable(long courseId, CancellationToken cancellationToken)
        => this.ToCourseBillingResponse(await commands.Send(new EnableCourseCommand(courseId), cancellationToken));

    [HttpGet("{courseId:long}/fees")]
    public async Task<IActionResult> ListFees(long courseId, CancellationToken cancellationToken)
        => this.ToCourseBillingResponse(await queries.Send(new ListCourseFeesQuery(courseId), cancellationToken));

    [HttpPost("{courseId:long}/fees")]
    public async Task<IActionResult> AddFee(long courseId, [FromBody] CreateCourseFeeRequest request, CancellationToken cancellationToken)
        => this.ToCourseBillingResponse(await commands.Send(new AddCourseFeeCommand(courseId, request), cancellationToken), created: true);

    [HttpPut("{courseId:long}/fees/{courseFeeId:long}")]
    public async Task<IActionResult> UpdateFee(long courseId, long courseFeeId, [FromBody] UpdateCourseFeeRequest request, CancellationToken cancellationToken)
        => this.ToCourseBillingResponse(await commands.Send(new UpdateCourseFeeCommand(courseId, courseFeeId, request), cancellationToken));

    [HttpDelete("{courseId:long}/fees/{courseFeeId:long}")]
    public async Task<IActionResult> DeleteFee(long courseId, long courseFeeId, CancellationToken cancellationToken)
        => this.ToCourseBillingResponse(await commands.Send(new DeleteCourseFeeCommand(courseId, courseFeeId), cancellationToken));

    [HttpGet("{courseId:long}/enrollments")]
    public async Task<IActionResult> ListEnrollments(long courseId, CancellationToken cancellationToken)
        => this.ToCourseBillingResponse(await queries.Send(new ListAdminCourseEnrollmentsQuery(courseId), cancellationToken));

    [HttpDelete("{courseId:long}/enrollments/{courseEnrollmentId:long}")]
    public async Task<IActionResult> RemoveEnrollment(long courseId, long courseEnrollmentId, CancellationToken cancellationToken)
        => this.ToCourseBillingResponse(await commands.Send(new RemoveAdminCourseEnrollmentCommand(courseId, courseEnrollmentId), cancellationToken));
}
