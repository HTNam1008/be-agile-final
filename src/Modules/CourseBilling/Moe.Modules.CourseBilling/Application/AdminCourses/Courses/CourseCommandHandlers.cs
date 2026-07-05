using Moe.Application.Abstractions.Audit;
using Moe.Application.Abstractions.Messaging;
using Moe.Application.Abstractions.Persistence;
using Moe.Modules.CourseBilling.Contracts.AdminCourses;
using Moe.Modules.CourseBilling.Domain.Courses;
using Moe.Modules.CourseBilling.IGateway.Repositories;
using Moe.Modules.IdentityPlatform.IGateway.Students;
using Moe.Modules.Notifications.Domain.Notifications;
using Moe.Modules.Notifications.IGateway.Notifications;
using Moe.SharedKernel.Results;
using Microsoft.Extensions.Logging;

namespace Moe.Modules.CourseBilling.Application.AdminCourses.Courses;

internal sealed class CreateCourseCommandHandler(AdminCourseAccess access, IAuditService audit, IUnitOfWork unitOfWork)
    : ICommandHandler<CreateCourseCommand, CourseDetailDto>
{
    public async Task<Result<CourseDetailDto>> Handle(CreateCourseCommand command, CancellationToken cancellationToken)
    {
        CreateCourseRequest request = command.Request;

        Result admin = access.RequireAdmin();
        if (admin.IsFailure)
        {
            return Result<CourseDetailDto>.Failure(admin.Error);
        }

        if (!access.CanAccessOrganization(request.OrganizationId))
        {
            return Result<CourseDetailDto>.Failure(access.OrganizationForbidden());
        }

        if (access.CurrentUser.UserAccountId is not long actorId)
        {
            return Result<CourseDetailDto>.Failure(CourseBillingErrors.ActorRequired);
        }

        DateTime utcNow = access.UtcNow();
        Result validation = await access.ValidateCourseInputAsync(
            request.OrganizationId,
            request.CourseCode,
            request.StartDate,
            request.EndDate,
            request.EnrollmentOpenAt,
            request.EnrollmentCloseAt,
            null,
            cancellationToken);
        if (validation.IsFailure)
        {
            return Result<CourseDetailDto>.Failure(validation.Error);
        }

        Course course = new(
            request.OrganizationId,
            request.CourseCode,
            request.CourseName,
            request.Description,
            request.StartDate,
            request.EndDate,
            request.EnrollmentOpenAt,
            request.EnrollmentCloseAt,
            actorId,
            utcNow,
            request.BeforeStartRefundPercentage,
            request.AfterStartRefundPercentage);

        await access.Courses.AddCourseAsync(course, cancellationToken);
        FeeComponent? gst = await access.Courses.FindActiveFeeComponentByCodeAsync(
            SystemFeeComponentCodes.Gst,
            cancellationToken);
        if (gst is not null && (!gst.IsSystemManaged || !gst.IsTaxComponent))
        {
            return Result<CourseDetailDto>.Failure(CourseErrors.GstComponentNotConfigured);
        }

        if (gst is not null)
        {
            CourseFee gstFee = new(course.Id, gst.Id, gst.DefaultValue, sequenceNumber: 999);
            await access.Courses.AddFeeAsync(gstFee, cancellationToken);
        }

        await audit.RecordSchoolActionAsync(
            new SchoolAuditContext(
                AuditActionCodes.CourseCreated,
                "Course",
                course.Id,
                course.OrganizationId,
                new SchoolAuditDetails("Course created", EntityDisplayName: course.CourseName)),
            cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return await access.LoadCourseDetailAsync(course.Id, cancellationToken);
    }
}

internal sealed class UpdateCourseCommandHandler(AdminCourseAccess access, IAuditService audit, IUnitOfWork unitOfWork)
    : ICommandHandler<UpdateCourseCommand, CourseDetailDto>
{
    public async Task<Result<CourseDetailDto>> Handle(UpdateCourseCommand command, CancellationToken cancellationToken)
    {
        UpdateCourseRequest request = command.Request;

        Result admin = access.RequireAdmin();
        if (admin.IsFailure)
        {
            return Result<CourseDetailDto>.Failure(admin.Error);
        }

        await access.DisableEndedCoursesOnAccessAsync(access.OrganizationScope, cancellationToken);

        Course? course = await access.Courses.FindCourseAsync(command.CourseId, cancellationToken);
        if (course is null)
        {
            return Result<CourseDetailDto>.Failure(CourseErrors.CourseNotFound);
        }

        if (!access.CanAccessOrganization(course.OrganizationId))
        {
            return Result<CourseDetailDto>.Failure(access.OrganizationForbidden());
        }

        if (course.IsDisabled)
        {
            return Result<CourseDetailDto>.Failure(CourseErrors.CourseDisabled);
        }

        if (access.CurrentUser.UserAccountId is not long actorId)
        {
            return Result<CourseDetailDto>.Failure(CourseBillingErrors.ActorRequired);
        }

        Result validation = await access.ValidateCourseInputAsync(
            course.OrganizationId,
            request.CourseCode,
            request.StartDate,
            request.EndDate,
            request.EnrollmentOpenAt,
            request.EnrollmentCloseAt,
            command.CourseId,
            cancellationToken);
        if (validation.IsFailure)
        {
            return Result<CourseDetailDto>.Failure(validation.Error);
        }

        DateTime utcNow = access.UtcNow();
        course.Update(
            request.CourseCode,
            request.CourseName,
            request.Description,
            request.StartDate,
            request.EndDate,
            request.EnrollmentOpenAt,
            request.EnrollmentCloseAt,
            actorId,
            utcNow);

        Result refundPolicy = course.UpdateRefundPolicy(
            request.BeforeStartRefundPercentage,
            request.AfterStartRefundPercentage,
            actorId,
            utcNow);
        if (refundPolicy.IsFailure)
        {
            return Result<CourseDetailDto>.Failure(refundPolicy.Error);
        }

        await access.Courses.SaveCourseAsync(course, cancellationToken);
        await audit.RecordSchoolActionAsync(
            new SchoolAuditContext(
                AuditActionCodes.CourseUpdated,
                "Course",
                course.Id,
                course.OrganizationId,
                new SchoolAuditDetails(
                    "Course edited",
                    EntityDisplayName: course.CourseName,
                    ChangedFields: ["courseCode", "courseName", "description", "dates", "enrollmentWindow", "refundPolicy"])),
            cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await access.LoadCourseDetailAsync(command.CourseId, cancellationToken);
    }
}

internal sealed class RemoveCourseCommandHandler(AdminCourseAccess access, IAuditService audit, IUnitOfWork unitOfWork)
    : ICommandHandler<RemoveCourseCommand, long>
{
    public async Task<Result<long>> Handle(RemoveCourseCommand command, CancellationToken cancellationToken)
    {
        Result admin = access.RequireAdmin();
        if (admin.IsFailure)
        {
            return Result<long>.Failure(admin.Error);
        }

        Course? course = await access.Courses.FindCourseAsync(command.CourseId, cancellationToken);
        if (course is null)
        {
            return Result<long>.Failure(CourseErrors.CourseNotFound);
        }

        if (!access.CanAccessOrganization(course.OrganizationId))
        {
            return Result<long>.Failure(access.OrganizationForbidden());
        }

        if (!course.IsDraft)
        {
            return Result<long>.Failure(CourseErrors.DraftRequiredForRemoval);
        }

        await access.Courses.RemoveDraftCourseAsync(command.CourseId, cancellationToken);
        await audit.RecordSchoolActionAsync(
            new SchoolAuditContext(
                AuditActionCodes.CourseDeleted,
                "Course",
                course.Id,
                course.OrganizationId,
                new SchoolAuditDetails("Course deleted", EntityDisplayName: course.CourseName)),
            cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<long>.Success(command.CourseId);
    }
}

internal sealed class PublishCourseCommandHandler(
    AdminCourseAccess access,
    IAuditService audit,
    IUnitOfWork unitOfWork,
    IStudentDirectory students,
    IStudentNotificationRecipientResolver notificationRecipients,
    INotificationWriter notificationWriter,
    ILogger<PublishCourseCommandHandler> logger)
    : ICommandHandler<PublishCourseCommand, CourseDetailDto>
{
    public async Task<Result<CourseDetailDto>> Handle(PublishCourseCommand command, CancellationToken cancellationToken)
    {
        Result admin = access.RequireAdmin();
        if (admin.IsFailure)
        {
            return Result<CourseDetailDto>.Failure(admin.Error);
        }

        Course? course = await access.Courses.FindCourseAsync(command.CourseId, cancellationToken);
        if (course is null)
        {
            return Result<CourseDetailDto>.Failure(CourseErrors.CourseNotFound);
        }

        if (!access.CanAccessOrganization(course.OrganizationId))
        {
            return Result<CourseDetailDto>.Failure(access.OrganizationForbidden());
        }

        if (course.IsDisabled)
        {
            return Result<CourseDetailDto>.Failure(CourseErrors.CourseDisabled);
        }

        if (!course.IsDraft)
        {
            return Result<CourseDetailDto>.Failure(CourseErrors.CourseNotDraft);
        }

        if (access.CurrentUser.UserAccountId is not long actorId)
        {
            return Result<CourseDetailDto>.Failure(CourseBillingErrors.ActorRequired);
        }

        CourseAggregate? aggregate = await access.Courses.GetCourseAggregateAsync(command.CourseId, cancellationToken);
        CoursePublishReadinessDto readiness = CourseMapper.BuildReadiness(aggregate!);
        if (!readiness.CanPublish)
        {
            return Result<CourseDetailDto>.Failure(
                new Error("COURSE.PUBLISH_VALIDATION_FAILED", string.Join(" ", readiness.Errors)));
        }

        DateTime utcNow = access.UtcNow();
        course.Publish(actorId, utcNow);
        await access.Courses.SaveCourseAsync(course, cancellationToken);
        await access.Courses.IssueBillsForUnbilledEnrollmentsAsync(
            command.CourseId,
            $"BILL-{utcNow:yyyyMMdd}",
            utcNow,
            access.TodayInSingapore().AddDays(30),
            cancellationToken);

        await audit.RecordSchoolActionAsync(
            new SchoolAuditContext(
                AuditActionCodes.CoursePublished,
                "Course",
                course.Id,
                course.OrganizationId,
                new SchoolAuditDetails(
                    "Course published",
                    EntityDisplayName: course.CourseName,
                    StatusTransition: new SchoolAuditStatusTransition("DRAFT", course.CourseStatusCode))),
            cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        await NotifyEnrollOpenAsync(course, cancellationToken);

        return await access.LoadCourseDetailAsync(command.CourseId, cancellationToken);
    }

    private async Task NotifyEnrollOpenAsync(Course course, CancellationToken cancellationToken)
    {
        IReadOnlyCollection<long> personIds = await students.FindActivePersonIdsByOrganizationAsync(
            course.OrganizationId,
            cancellationToken);

        if (personIds.Count == 0)
        {
            return;
        }

        System.Collections.Generic.List<long> userAccountIds = new();
        foreach (long personId in personIds)
        {
            long? userAccountId = await notificationRecipients.FindUserAccountIdByPersonIdAsync(personId, cancellationToken);
            if (userAccountId.HasValue)
            {
                userAccountIds.Add(userAccountId.Value);
            }
        }

        foreach (long userAccountId in userAccountIds.Distinct())
        {
            await notificationWriter.CreateForBusinessFlowAsync(
                new NotificationCreateRequest(
                    userAccountId,
                    NotificationTypeCode.EnrollOpen,
                    $"Course Enrollment Open: {course.CourseCode}",
                    $"Registration for {course.CourseName} is open until {FormatSingaporeDateTime(course.EnrollmentCloseAtUtc)}."),
                logger,
                "Course publish enrollment open",
                cancellationToken);
        }
    }

    private static string FormatSingaporeDateTime(DateTime utc)
        => utc.AddHours(8).ToString("dd/MM/yyyy, HH:mm");
}

internal sealed class DisableCourseCommandHandler(
    AdminCourseAccess access,
    IAuditService audit,
    IUnitOfWork unitOfWork,
    INotificationWriter notificationWriter,
    IStudentNotificationRecipientResolver notificationRecipients,
    ILogger<DisableCourseCommandHandler> logger)
    : ICommandHandler<DisableCourseCommand, CourseDetailDto>
{
    public async Task<Result<CourseDetailDto>> Handle(DisableCourseCommand command, CancellationToken cancellationToken)
    {
        Result<CourseDetailDto> result = await access.SetCourseEnabledStateAsync(command.CourseId, enabled: false, cancellationToken);
        if (result.IsSuccess)
        {
            await audit.RecordSchoolActionAsync(
                new SchoolAuditContext(
                    AuditActionCodes.CourseDisabled,
                    "Course",
                    result.Value.CourseId,
                    result.Value.OrganizationId,
                    new SchoolAuditDetails("Course disabled", EntityDisplayName: result.Value.CourseName)),
                cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            await NotifyCourseDisabledAsync(result.Value.CourseId, result.Value.CourseCode, result.Value.CourseName, cancellationToken);
        }

        return result;
    }

    private async Task NotifyCourseDisabledAsync(long courseId, string courseCode, string courseName, CancellationToken cancellationToken)
    {
        IReadOnlyList<AdminCourseEnrollmentDto> enrollments = await access.Courses.ListEnrollmentsAsync(courseId, cancellationToken);
        if (enrollments.Count == 0)
        {
            return;
        }

        foreach (AdminCourseEnrollmentDto enrollment in enrollments.Where(x => x.EnrollmentStatusCode is not CourseEnrollmentStatusCodes.Cancelled and not CourseEnrollmentStatusCodes.Exited))
        {
            long? userAccountId = await notificationRecipients.FindUserAccountIdByPersonIdAsync(enrollment.PersonId, cancellationToken);
            if (userAccountId is null)
            {
                continue;
            }

            await notificationWriter.CreateForBusinessFlowAsync(
                new NotificationCreateRequest(
                    userAccountId.Value,
                    NotificationTypeCode.CourseDisabled,
                    $"Course Disabled: {courseCode}",
                    $"Reason: MOE administrative action. The course {courseCode} ({courseName}) is currently unavailable."),
                logger,
                "Course disable enrolled student notification",
                cancellationToken);
        }
    }
}
internal sealed class EnableCourseCommandHandler(AdminCourseAccess access, IAuditService audit, IUnitOfWork unitOfWork)
    : ICommandHandler<EnableCourseCommand, CourseDetailDto>
{
    public async Task<Result<CourseDetailDto>> Handle(EnableCourseCommand command, CancellationToken cancellationToken)
    {
        Result<CourseDetailDto> result = await access.SetCourseEnabledStateAsync(command.CourseId, enabled: true, cancellationToken);
        if (result.IsSuccess)
        {
            await audit.RecordSchoolActionAsync(
                new SchoolAuditContext(
                    AuditActionCodes.CourseEnabled,
                    "Course",
                    result.Value.CourseId,
                    result.Value.OrganizationId,
                    new SchoolAuditDetails("Course enabled", EntityDisplayName: result.Value.CourseName)),
                cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return result;
    }
}





