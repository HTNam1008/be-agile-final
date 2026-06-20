using Moe.Application.Abstractions.Clock;
using Moe.Application.Abstractions.Messaging;
using Moe.Application.Abstractions.Security;
using Moe.Modules.CourseBilling.Domain.Courses;
using Moe.Modules.CourseBilling.IGateway.Repositories;
using Moe.SharedKernel.Results;

namespace Moe.Modules.CourseBilling.Application.Enrollments.AdminEnrollPerson;

internal sealed class AdminEnrollPersonHandler(
    ICourseEnrollmentRepository enrollments,
    ICurrentUser currentUser,
    IClock clock) : ICommandHandler<AdminEnrollPersonCommand, CourseEnrollmentResponse>
{
    private const long MoeHeadquartersOrganizationId = 1;

    public async Task<Result<CourseEnrollmentResponse>> Handle(
        AdminEnrollPersonCommand command,
        CancellationToken cancellationToken)
    {
        long? actorId = currentUser.UserAccountId;

        if (actorId is null)
        {
            return Result<CourseEnrollmentResponse>.Failure(CourseBillingErrors.ActorRequired);
        }

        if (!await enrollments.PersonExistsAsync(command.PersonId, cancellationToken))
        {
            return Result<CourseEnrollmentResponse>.Failure(CourseBillingErrors.PersonNotFound);
        }

        Course? course = await enrollments.FindCourseAsync(command.CourseId, cancellationToken);

        if (course is null)
        {
            return Result<CourseEnrollmentResponse>.Failure(CourseBillingErrors.CourseNotFound);
        }

        if (course.IsDisabled)
        {
            return Result<CourseEnrollmentResponse>.Failure(CourseErrors.CourseDisabled);
        }

        DateTime utcNow = clock.UtcNow.UtcDateTime;

        if (utcNow < course.EnrollmentOpenAtUtc || utcNow > course.EnrollmentCloseAtUtc)
        {
            return Result<CourseEnrollmentResponse>.Failure(CourseErrors.EnrollmentWindowClosed);
        }

        bool hasGlobalScope = currentUser.HasPermission("ORG_VIEW_ALL")
            || currentUser.OrganizationUnitId == MoeHeadquartersOrganizationId
            || currentUser.OrganizationUnitIds.Contains(MoeHeadquartersOrganizationId);

        if (!hasGlobalScope
            && currentUser.OrganizationUnitId != course.OrganizationId
            && !currentUser.OrganizationUnitIds.Contains(course.OrganizationId))
        {
            return Result<CourseEnrollmentResponse>.Failure(CourseBillingErrors.CourseOrganizationForbidden);
        }

        DateOnly today = DateOnly.FromDateTime(utcNow);

        if (!await enrollments.PersonHasActiveSchoolEnrollmentAsync(
            command.PersonId,
            course.OrganizationId,
            today,
            cancellationToken))
        {
            return Result<CourseEnrollmentResponse>.Failure(CourseBillingErrors.PersonNotInCourseOrganization);
        }

        if (await enrollments.ExistsAsync(command.PersonId, command.CourseId, cancellationToken))
        {
            return Result<CourseEnrollmentResponse>.Failure(CourseBillingErrors.DuplicateEnrollment);
        }

        Result<CourseEnrollment> enrollmentResult = CourseEnrollment.EnrollByAdmin(
            command.PersonId,
            command.CourseId,
            actorId.Value,
            utcNow);

        if (enrollmentResult.IsFailure)
        {
            return Result<CourseEnrollmentResponse>.Failure(enrollmentResult.Error);
        }

        if (course.IsDraft)
        {
            await enrollments.AddEnrollmentAsync(enrollmentResult.Value, cancellationToken);
            return Result<CourseEnrollmentResponse>.Success(ToPendingResponse(enrollmentResult.Value));
        }

        IReadOnlyCollection<CourseFeeBillingLine> feeLines = await enrollments.ListActiveCourseFeesAsync(
            command.CourseId,
            cancellationToken);

        if (feeLines.Count == 0)
        {
            return Result<CourseEnrollmentResponse>.Failure(CourseBillingErrors.CourseFeesNotConfigured);
        }

        CourseEnrollmentBillingResult billingResult = await enrollments.AddEnrollmentAndIssueBillAsync(
            enrollmentResult.Value,
            CreateBillNumber(utcNow),
            utcNow,
            DateOnly.FromDateTime(utcNow).AddDays(30),
            feeLines,
            cancellationToken);

        return Result<CourseEnrollmentResponse>.Success(ToResponse(billingResult));
    }

    private static CourseEnrollmentResponse ToResponse(CourseEnrollmentBillingResult result)
        => new(
            result.Enrollment.Id,
            result.Enrollment.PersonId,
            result.Enrollment.CourseId,
            result.Enrollment.EnrollmentSourceCode,
            result.Enrollment.EnrolledByLoginAccountId,
            result.Enrollment.EnrollmentStatusCode,
            result.Bill.Id,
            result.Bill.BillNumber,
            result.Bill.BillStatusCode,
            result.BillLineCount,
            result.Bill.GrossAmount,
            result.Bill.NetPayableAmount,
            result.Bill.OutstandingAmount);

    private static CourseEnrollmentResponse ToPendingResponse(CourseEnrollment enrollment)
        => new(
            enrollment.Id,
            enrollment.PersonId,
            enrollment.CourseId,
            enrollment.EnrollmentSourceCode,
            enrollment.EnrolledByLoginAccountId,
            enrollment.EnrollmentStatusCode,
            null,
            null,
            null,
            0,
            0m,
            0m,
            0m);

    private static string CreateBillNumber(DateTime utcNow)
        => $"BILL-{utcNow:yyyyMMdd}-{Guid.NewGuid():N}"[..30].ToUpperInvariant();
}
