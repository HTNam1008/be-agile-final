using Moe.Application.Abstractions.Messaging;
using Moe.Modules.CourseBilling.Contracts.AdminCourses;
using Moe.Modules.CourseBilling.Domain.Courses;
using Moe.Modules.CourseBilling.IGateway.Repositories;
using Moe.SharedKernel.Results;

namespace Moe.Modules.CourseBilling.Application.AdminCourses.Courses;

internal sealed class CreateCourseCommandHandler(AdminCourseAccess access)
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
        return await access.LoadCourseDetailAsync(course.Id, cancellationToken);
    }
}

internal sealed class UpdateCourseCommandHandler(AdminCourseAccess access)
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
        return await access.LoadCourseDetailAsync(command.CourseId, cancellationToken);
    }
}

internal sealed class RemoveCourseCommandHandler(AdminCourseAccess access)
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
        return Result<long>.Success(command.CourseId);
    }
}

internal sealed class PublishCourseCommandHandler(AdminCourseAccess access)
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
            DateOnly.FromDateTime(utcNow).AddDays(30),
            cancellationToken);

        return await access.LoadCourseDetailAsync(command.CourseId, cancellationToken);
    }
}

internal sealed class DisableCourseCommandHandler(AdminCourseAccess access)
    : ICommandHandler<DisableCourseCommand, CourseDetailDto>
{
    public async Task<Result<CourseDetailDto>> Handle(DisableCourseCommand command, CancellationToken cancellationToken)
    {
        return await access.SetCourseEnabledStateAsync(command.CourseId, enabled: false, cancellationToken);
    }
}

internal sealed class EnableCourseCommandHandler(AdminCourseAccess access)
    : ICommandHandler<EnableCourseCommand, CourseDetailDto>
{
    public async Task<Result<CourseDetailDto>> Handle(EnableCourseCommand command, CancellationToken cancellationToken)
    {
        return await access.SetCourseEnabledStateAsync(command.CourseId, enabled: true, cancellationToken);
    }
}
