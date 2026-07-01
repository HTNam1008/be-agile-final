using Moe.Application.Abstractions.Audit;
using Moe.Application.Abstractions.Messaging;
using Moe.Application.Abstractions.Persistence;
using Moe.Modules.CourseBilling.Contracts.AdminCourses;
using Moe.Modules.CourseBilling.Domain.Courses;
using Moe.Modules.CourseBilling.IGateway.Fas;
using Moe.Modules.CourseBilling.IGateway.Repositories;
using Moe.Modules.IdentityPlatform.IGateway.Students;
using Moe.Modules.Notifications.Domain.Notifications;
using Moe.Modules.Notifications.IGateway.Notifications;
using Moe.SharedKernel.Results;

namespace Moe.Modules.CourseBilling.Application.AdminCourses.Enrollments;

internal sealed class ListAdminCourseEnrollmentsQueryHandler(AdminCourseAccess access)
    : IQueryHandler<ListAdminCourseEnrollmentsQuery, IReadOnlyList<AdminCourseEnrollmentDto>>
{
    public async Task<Result<IReadOnlyList<AdminCourseEnrollmentDto>>> Handle(
        ListAdminCourseEnrollmentsQuery query,
        CancellationToken cancellationToken)
    {
        Result<Course> course = await access.RequireCourseAsync(query.CourseId, cancellationToken);
        if (course.IsFailure)
        {
            return Result<IReadOnlyList<AdminCourseEnrollmentDto>>.Failure(course.Error);
        }

        IReadOnlyList<AdminCourseEnrollmentDto> enrollments = await access.Courses.ListEnrollmentsAsync(
            query.CourseId,
            cancellationToken);
        return Result<IReadOnlyList<AdminCourseEnrollmentDto>>.Success(enrollments);
    }
}

internal sealed class RemoveAdminCourseEnrollmentCommandHandler(
    AdminCourseAccess access,
    IFasCourseSubsidyGateway fasSubsidies,
    IAuditService audit,
    IUnitOfWork unitOfWork,
    IStudentDirectory students,
    ISchoolAdminNotificationRecipientResolver schoolAdminRecipients,
    INotificationWriter notificationWriter)
    : ICommandHandler<RemoveAdminCourseEnrollmentCommand, AdminCourseEnrollmentDto>
{
    public async Task<Result<AdminCourseEnrollmentDto>> Handle(
        RemoveAdminCourseEnrollmentCommand command,
        CancellationToken cancellationToken)
    {
        Result<Course> course = await access.RequireCourseAsync(command.CourseId, cancellationToken);
        if (course.IsFailure)
        {
            return Result<AdminCourseEnrollmentDto>.Failure(course.Error);
        }

        CourseEnrollment? enrollment = await access.Courses.FindEnrollmentAsync(
            command.CourseEnrollmentId,
            cancellationToken);
        if (enrollment is null || enrollment.CourseId != command.CourseId)
        {
            return Result<AdminCourseEnrollmentDto>.Failure(CourseErrors.EnrollmentNotFound);
        }

        Result cancellation = await access.Courses.CancelEnrollmentAndBillAsync(
            enrollment,
            access.UtcNow(),
            cancellationToken);
        if (cancellation.IsFailure)
        {
            return Result<AdminCourseEnrollmentDto>.Failure(cancellation.Error);
        }

        await fasSubsidies.CancelPendingRedemptionsForEnrollmentAsync(
            enrollment.Id,
            access.UtcNow(),
            cancellationToken);

        await audit.RecordSchoolActionAsync(
            new SchoolAuditContext(
                AuditActionCodes.CourseEnrollmentRemovedByAdmin,
                "CourseEnrollment",
                enrollment.Id,
                course.Value.OrganizationId,
                new SchoolAuditDetails(
                    "Student removed from course",
                    EntityDisplayName: course.Value.CourseName,
                    RelatedIds: new Dictionary<string, long>
                    {
                        ["studentPersonId"] = enrollment.PersonId,
                        ["courseId"] = enrollment.CourseId
                    })),
            cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

                await NotifyCourseExitAsync(
            course.Value.OrganizationId,
            enrollment.PersonId,
            course.Value.CourseName,
            cancellationToken);

        return Result<AdminCourseEnrollmentDto>.Success(new AdminCourseEnrollmentDto(
            enrollment.Id,
            enrollment.CourseId,
            enrollment.PersonId,
            null,
            null,
            null,
            null,
            enrollment.EnrollmentSourceCode,
            enrollment.EnrolledByLoginAccountId,
            enrollment.EnrolledAtUtc,
            enrollment.EnrollmentStatusCode));
    }

    private async Task NotifyCourseExitAsync(
        long organizationId,
        long personId,
        string courseName,
        CancellationToken cancellationToken)
    {
        StudentSummary? student = await students.FindByPersonIdAsync(personId, cancellationToken);
        if (student is null)
        {
            return;
        }

        IReadOnlyCollection<long> userAccountIds = await schoolAdminRecipients.FindUserAccountIdsByOrganizationIdAsync(
            organizationId,
            cancellationToken);

        foreach (long userAccountId in userAccountIds.Distinct())
        {
            await notificationWriter.CreateAsync(
                new NotificationCreateRequest(
                    userAccountId,
                    NotificationTypeCode.CourseExit,
                    "Course Completed",
                    $"Result: ADMIN_REMOVED. {student.DisplayName} was removed from {courseName}."),
                cancellationToken);
        }
    }
}










