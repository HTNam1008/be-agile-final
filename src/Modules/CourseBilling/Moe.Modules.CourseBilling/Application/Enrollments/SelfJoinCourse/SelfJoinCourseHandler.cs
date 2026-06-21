using Moe.Application.Abstractions.Clock;
using Moe.Application.Abstractions.Messaging;
using Moe.Application.Abstractions.Security;
using Moe.Modules.CourseBilling.Contracts.Enrollments;
using Moe.Modules.CourseBilling.Domain.Courses;
using Moe.Modules.CourseBilling.IGateway.Repositories;
using Moe.SharedKernel.Results;

namespace Moe.Modules.CourseBilling.Application.Enrollments.SelfJoinCourse;

internal sealed class SelfJoinCourseHandler(
    ICourseEnrollmentRepository enrollments,
    ICurrentUser currentUser,
    IStudentAccessControl studentAccess,
    IClock clock) : ICommandHandler<SelfJoinCourseCommand, CourseEnrollmentResponse>
{
    public async Task<Result<CourseEnrollmentResponse>> Handle(
        SelfJoinCourseCommand command,
        CancellationToken cancellationToken)
    {
        long? actorId = currentUser.UserAccountId;

        if (actorId is null)
        {
            return Result<CourseEnrollmentResponse>.Failure(CourseBillingErrors.ActorRequired);
        }

        long? personId = studentAccess.PersonId;

        if (personId is null || !studentAccess.IsStudent)
        {
            return Result<CourseEnrollmentResponse>.Failure(CourseBillingErrors.StudentIdentityRequired);
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

        if (!course.IsPublished)
        {
            return Result<CourseEnrollmentResponse>.Failure(CourseErrors.CourseNotPublished);
        }

        DateTime utcNow = clock.UtcNow.UtcDateTime;

        if (utcNow < course.EnrollmentOpenAtUtc || utcNow > course.EnrollmentCloseAtUtc)
        {
            return Result<CourseEnrollmentResponse>.Failure(CourseErrors.EnrollmentWindowClosed);
        }

        if (!await studentAccess.CanUseSchoolServiceAsync(course.OrganizationId, cancellationToken))
        {
            return Result<CourseEnrollmentResponse>.Failure(CourseBillingErrors.PersonNotInCourseOrganization);
        }

        if (await enrollments.ExistsAsync(personId.Value, command.CourseId, cancellationToken))
        {
            return Result<CourseEnrollmentResponse>.Failure(CourseBillingErrors.DuplicateEnrollment);
        }

        IReadOnlyCollection<CourseFeeBillingLine> feeLines = await enrollments.ListActiveCourseFeesAsync(
            command.CourseId,
            cancellationToken);

        if (feeLines.Count == 0)
        {
            return Result<CourseEnrollmentResponse>.Failure(CourseBillingErrors.CourseFeesNotConfigured);
        }

        Result<CourseEnrollment> enrollmentResult = CourseEnrollment.JoinSelf(
            personId.Value,
            command.CourseId,
            actorId.Value,
            utcNow);

        if (enrollmentResult.IsFailure)
        {
            return Result<CourseEnrollmentResponse>.Failure(enrollmentResult.Error);
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

    private static string CreateBillNumber(DateTime utcNow)
        => $"BILL-{utcNow:yyyyMMdd}-{Guid.NewGuid():N}"[..30].ToUpperInvariant();
}
