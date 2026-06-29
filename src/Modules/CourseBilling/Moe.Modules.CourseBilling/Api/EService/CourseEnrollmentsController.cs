using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Moe.Application.Abstractions.Messaging;
using Moe.Infrastructure.Shared.Security;
using Moe.Modules.CourseBilling.Api;
using Moe.Modules.CourseBilling.Application.Enrollments.CourseContent;
using Moe.Modules.CourseBilling.Application.Enrollments.SelfJoinCourse;
using Moe.Modules.CourseBilling.Contracts.Enrollments;

namespace Moe.Modules.CourseBilling.Api.EService;

[ApiController]
[ApiVersion(1.0)]
[Route("api/eservice/v{version:apiVersion}/course-enrollments")]
[Authorize(Policy = AuthorizationPolicies.EServicePortal)]
[EnableCors("EServiceCors")]
public sealed class CourseEnrollmentsController(
    ICommandDispatcher commands,
    IQueryDispatcher queries) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> JoinCourse(
        [FromBody] SelfJoinCourseRequest request,
        CancellationToken cancellationToken)
    {
        var result = await commands.Send(
            new SelfJoinCourseCommand(
                request.CourseId,
                request.CoursePaymentPlanId,
                request.FasApplicationSchemeIds),
            cancellationToken);
        return this.ToCourseBillingResponse(result, created: true);
    }

    [HttpPut("{enrollmentId:long}/payment-plan")]
    public async Task<IActionResult> ChangePaymentPlan(
        long enrollmentId,
        [FromBody] ChangePaymentPlanRequest request,
        CancellationToken cancellationToken)
        => this.ToCourseBillingResponse(await commands.Send(
            new ChangeEnrollmentPaymentPlanCommand(
                enrollmentId,
                request.CoursePaymentPlanId,
                request.FasApplicationSchemeIds),
            cancellationToken));

    [HttpPost("{enrollmentId:long}/payment-plan-preview")]
    public async Task<IActionResult> PreviewPaymentPlanBill(
        long enrollmentId,
        [FromBody] PreviewPaymentPlanBillRequest request,
        CancellationToken cancellationToken)
        => this.ToCourseBillingResponse(await queries.Send(
            new PreviewPaymentPlanBillQuery(
                enrollmentId,
                request.CoursePaymentPlanId,
                request.FasApplicationSchemeIds),
            cancellationToken));

    [HttpGet("{enrollmentId:long}/content")]
    public async Task<IActionResult> GetContent(
        long enrollmentId,
        CancellationToken cancellationToken)
        => this.ToCourseBillingResponse(await queries.Send(
            new GetStudentCourseContentQuery(enrollmentId),
            cancellationToken));

    [HttpGet("{enrollmentId:long}/materials/{courseMaterialId:long}")]
    public async Task<IActionResult> DownloadMaterial(
        long enrollmentId,
        long courseMaterialId,
        [FromQuery] string? preview,
        CancellationToken cancellationToken)
    {
        var result = await queries.Send(
            new DownloadStudentCourseMaterialQuery(
                enrollmentId,
                courseMaterialId,
                string.Equals(preview, "pdf", StringComparison.OrdinalIgnoreCase)),
            cancellationToken);
        return result.IsFailure
            ? this.ToCourseBillingResponse(result)
            : File(result.Value.Content, result.Value.ContentType, result.Value.FileName);
    }
}
