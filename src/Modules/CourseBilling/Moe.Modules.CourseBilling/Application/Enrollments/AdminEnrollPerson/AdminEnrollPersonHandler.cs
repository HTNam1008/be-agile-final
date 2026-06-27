using Moe.Application.Abstractions.Audit;
using Moe.Application.Abstractions.Clock;
using Moe.Application.Abstractions.Messaging;
using Moe.Application.Abstractions.Persistence;
using Moe.Application.Abstractions.Security;
using Moe.Modules.CourseBilling.Contracts.Enrollments;
using Moe.Modules.CourseBilling.Domain.Courses;
using Moe.Modules.CourseBilling.IGateway.Repositories;
using Moe.SharedKernel.Results;

namespace Moe.Modules.CourseBilling.Application.Enrollments.AdminEnrollPerson;

internal sealed class AdminEnrollPersonHandler(
    ICourseEnrollmentRepository enrollments,
    ICurrentUser currentUser,
    IAdminAccessControl adminAccess,
    IClock clock,
    IAuditService audit,
    IUnitOfWork unitOfWork) : ICommandHandler<AdminEnrollPersonCommand, CourseEnrollmentResponse>
{
    public async Task<Result<CourseEnrollmentResponse>> Handle(
        AdminEnrollPersonCommand command,
        CancellationToken cancellationToken)
    {
        long? actorId = currentUser.UserAccountId;

        if (actorId is null)
        {
            return Result<CourseEnrollmentResponse>.Failure(CourseBillingErrors.ActorRequired);
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

        if (!adminAccess.CanAccessOrganization(course.OrganizationId))
        {
            return Result<CourseEnrollmentResponse>.Failure(CourseBillingErrors.CourseOrganizationForbidden);
        }

        DateOnly today = DateOnly.FromDateTime(utcNow);
        long? personId = await enrollments.FindActiveStudentPersonIdAsync(
            command.StudentNumber,
            course.OrganizationId,
            today,
            cancellationToken);

        if (personId is null)
        {
            return Result<CourseEnrollmentResponse>.Failure(CourseBillingErrors.PersonNotFound);
        }

        if (!await enrollments.PersonHasActiveSchoolEnrollmentAsync(
            personId.Value,
            course.OrganizationId,
            today,
            cancellationToken))
        {
            return Result<CourseEnrollmentResponse>.Failure(CourseBillingErrors.PersonNotInCourseOrganization);
        }

        if (await enrollments.ExistsAsync(personId.Value, command.CourseId, cancellationToken))
        {
            return Result<CourseEnrollmentResponse>.Failure(CourseBillingErrors.DuplicateEnrollment);
        }

        Result<CourseEnrollment> enrollmentResult = CourseEnrollment.EnrollByAdminPendingPlanSelection(
            personId.Value,
            command.CourseId,
            actorId.Value,
            utcNow,
            course.BeforeStartRefundPercentage,
            course.AfterStartRefundPercentage);

        if (enrollmentResult.IsFailure)
        {
            return Result<CourseEnrollmentResponse>.Failure(enrollmentResult.Error);
        }

        await enrollments.AddEnrollmentAsync(enrollmentResult.Value, cancellationToken);
        await RecordEnrollmentAuditAsync(course, enrollmentResult.Value, cancellationToken);
        return Result<CourseEnrollmentResponse>.Success(ToPendingResponse(enrollmentResult.Value));
    }

    private async Task RecordEnrollmentAuditAsync(
        Course course,
        CourseEnrollment enrollment,
        CancellationToken cancellationToken)
    {
        await audit.RecordSchoolActionAsync(
            new SchoolAuditContext(
                AuditActionCodes.CourseEnrollmentCreatedByAdmin,
                "CourseEnrollment",
                enrollment.Id,
                course.OrganizationId,
                new SchoolAuditDetails(
                    "Student manually enrolled into course",
                    EntityDisplayName: course.CourseName,
                    RelatedIds: new Dictionary<string, long>
                    {
                        ["studentPersonId"] = enrollment.PersonId,
                        ["courseId"] = course.Id
                    })),
            cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

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
            0m,
            []);
}
