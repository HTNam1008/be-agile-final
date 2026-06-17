using Moe.Application.Abstractions.Clock;
using Moe.Application.Abstractions.Messaging;
using Moe.Application.Abstractions.Security;
using Moe.Modules.CourseBilling.Domain.Courses;
using Moe.Modules.CourseBilling.IGateway.Repositories;
using Moe.SharedKernel.Results;
using Microsoft.Extensions.Hosting;

namespace Moe.Modules.CourseBilling.Application.Enrollments.SelfJoinCourse;

internal sealed class SelfJoinCourseHandler(
    ICourseEnrollmentRepository enrollments,
    ICurrentUser currentUser,
    IClock clock,
    IHostEnvironment environment) : ICommandHandler<SelfJoinCourseCommand, CourseEnrollmentResponse>
{
    private const long DevelopmentStudentLoginAccountId = 1003;
    private const long DevelopmentStudentPersonId = 2001;

    public async Task<Result<CourseEnrollmentResponse>> Handle(
        SelfJoinCourseCommand command,
        CancellationToken cancellationToken)
    {
        long? currentActorId = currentUser.UserAccountId;
        long? actorId = currentActorId ?? (environment.IsDevelopment() ? DevelopmentStudentLoginAccountId : null);

        if (actorId is null)
        {
            return Result<CourseEnrollmentResponse>.Failure(CourseBillingErrors.ActorRequired);
        }

        long? currentPersonId = currentUser.PersonId;
        long? personId = currentPersonId ?? (environment.IsDevelopment() ? DevelopmentStudentPersonId : null);

        if (personId is null)
        {
            return Result<CourseEnrollmentResponse>.Failure(CourseBillingErrors.StudentIdentityRequired);
        }

        long? courseOrganizationId = await enrollments.FindCourseOrganizationIdAsync(command.CourseId, cancellationToken);

        if (courseOrganizationId is null)
        {
            return Result<CourseEnrollmentResponse>.Failure(CourseBillingErrors.CourseNotFound);
        }

        DateTime utcNow = clock.UtcNow.UtcDateTime;
        DateOnly today = DateOnly.FromDateTime(utcNow);

        if (!await enrollments.PersonHasActiveSchoolEnrollmentAsync(
            personId.Value,
            courseOrganizationId.Value,
            today,
            cancellationToken))
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
            result.Enrollment.EnrolledByLoginAccountId!.Value,
            result.Enrollment.EnrollmentStatusCode,
            result.Bill.Id,
            result.Bill.BillNumber,
            result.Bill.BillStatusCode,
            result.Bill.GrossAmount,
            result.Bill.NetPayableAmount,
            result.Bill.OutstandingAmount);

    private static string CreateBillNumber(DateTime utcNow)
        => $"BILL-{utcNow:yyyyMMdd}-{Guid.NewGuid():N}"[..30].ToUpperInvariant();
}
