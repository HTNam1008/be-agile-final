using Moe.Application.Abstractions.Clock;
using Moe.Application.Abstractions.Security;
using Moe.Modules.CourseBilling.Application.AdminCourses.Courses;
using Moe.Modules.CourseBilling.Contracts.AdminCourses;
using Moe.Modules.CourseBilling.Domain.Courses;
using Moe.Modules.CourseBilling.IGateway.Repositories;
using Moe.Modules.CourseBilling.Infrastructure.Security;
using Moe.SharedKernel.Results;

namespace Moe.Modules.CourseBilling.Application.AdminCourses;

internal sealed class AdminCourseAccess(
    IAdminCourseRepository courses,
    ICurrentAdminContext currentAdmin,
    ICurrentUser currentUser,
    IAdminAccessControl adminAccess,
    IClock clock)
{
    public IAdminCourseRepository Courses => courses;
    public ICurrentUser CurrentUser => currentUser;

    public DateTime UtcNow() => clock.UtcNow.UtcDateTime;

    public Result RequireAdmin()
        => currentAdmin.IsAdmin ? Result.Success() : Result.Failure(CourseErrors.AdminRequired);

    public IReadOnlyCollection<long>? OrganizationScope
        => adminAccess.IsHqAdmin
            ? null
            : adminAccess.ScopedOrganizationIds;

    public bool CanAccessOrganization(long organizationId)
        => adminAccess.CanAccessOrganization(organizationId);

    public bool IsHqAdmin => currentAdmin.IsHqAdmin;

    public Error OrganizationForbidden()
        => new("COURSE.ORGANIZATION_FORBIDDEN", "User does not have access to this organization unit.");

    public async Task DisableEndedCoursesOnAccessAsync(
        IReadOnlyCollection<long>? scopedOrganizationIds,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserAccountId is not long actorId)
        {
            return;
        }

        DateTime utcNow = UtcNow();
        await courses.DisableEndedCoursesAsync(
            DateOnly.FromDateTime(utcNow),
            utcNow,
            actorId,
            scopedOrganizationIds,
            cancellationToken);
    }

    public async Task<Result<Course>> RequireCourseAsync(long courseId, CancellationToken cancellationToken)
    {
        Result admin = RequireAdmin();
        if (admin.IsFailure)
        {
            return Result<Course>.Failure(admin.Error);
        }

        Course? course = await courses.FindCourseAsync(courseId, cancellationToken);
        if (course is null)
        {
            return Result<Course>.Failure(CourseErrors.CourseNotFound);
        }

        if (!CanAccessOrganization(course.OrganizationId))
        {
            return Result<Course>.Failure(OrganizationForbidden());
        }

        return Result<Course>.Success(course);
    }

    public async Task<Result<Course>> RequireMutableCourseAsync(long courseId, CancellationToken cancellationToken)
    {
        Result<Course> course = await RequireCourseAsync(courseId, cancellationToken);
        if (course.IsFailure)
        {
            return course;
        }

        return course.Value.IsDisabled
            ? Result<Course>.Failure(CourseErrors.CourseDisabled)
            : course;
    }

    public async Task<Result<CourseDetailDto>> LoadCourseDetailAsync(
        long courseId,
        CancellationToken cancellationToken)
    {
        CourseAggregate? aggregate = await courses.GetCourseAggregateAsync(courseId, cancellationToken);
        return aggregate is null
            ? Result<CourseDetailDto>.Failure(CourseErrors.CourseNotFound)
            : Result<CourseDetailDto>.Success(CourseMapper.ToDetail(aggregate));
    }

    public async Task<Result<CourseDetailDto>> SetCourseEnabledStateAsync(
        long courseId,
        bool enabled,
        CancellationToken cancellationToken)
    {
        Result admin = RequireAdmin();
        if (admin.IsFailure)
        {
            return Result<CourseDetailDto>.Failure(admin.Error);
        }

        Course? course = await courses.FindCourseAsync(courseId, cancellationToken);
        if (course is null)
        {
            return Result<CourseDetailDto>.Failure(CourseErrors.CourseNotFound);
        }

        if (!CanAccessOrganization(course.OrganizationId))
        {
            return Result<CourseDetailDto>.Failure(OrganizationForbidden());
        }

        if (currentUser.UserAccountId is not long actorId)
        {
            return Result<CourseDetailDto>.Failure(CourseBillingErrors.ActorRequired);
        }

        if (enabled)
        {
            course.Enable(actorId, UtcNow());
        }
        else
        {
            course.Disable(actorId, UtcNow());
        }

        await courses.SaveCourseAsync(course, cancellationToken);

        return await LoadCourseDetailAsync(courseId, cancellationToken);
    }

    public async Task<Result> ValidateCourseInputAsync(
        long organizationId,
        string courseCode,
        DateOnly startDate,
        DateOnly endDate,
        DateTime enrollmentOpenAt,
        DateTime enrollmentCloseAt,
        long? excludeCourseId,
        CancellationToken cancellationToken)
    {
        DateTime utcNow = UtcNow();
        DateOnly today = DateOnly.FromDateTime(utcNow);
        DateTime currentMinute = new(
            utcNow.Year,
            utcNow.Month,
            utcNow.Day,
            utcNow.Hour,
            utcNow.Minute,
            0,
            DateTimeKind.Utc);

        if (startDate < today || endDate < today)
        {
            return Result.Failure(CourseErrors.CourseDateInPast);
        }

        if (startDate > endDate)
        {
            return Result.Failure(CourseErrors.InvalidDateRange);
        }

        if (enrollmentCloseAt < currentMinute)
        {
            return Result.Failure(CourseErrors.EnrollmentDateInPast);
        }

        if (enrollmentOpenAt > enrollmentCloseAt)
        {
            return Result.Failure(CourseErrors.InvalidEnrollmentWindow);
        }

        DateTime courseStartsAtUtc = startDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        if (enrollmentCloseAt >= courseStartsAtUtc)
        {
            return Result.Failure(CourseErrors.EnrollmentMustCloseBeforeCourseStarts);
        }

        if (organizationId <= 0)
        {
            return Result.Failure(new Error("COURSE.ORGANIZATION_REQUIRED", "A valid organization unit is required."));
        }

        if (await courses.CourseCodeExistsAsync(organizationId, courseCode, excludeCourseId, cancellationToken))
        {
            return Result.Failure(CourseErrors.DuplicateCourseCode);
        }

        return Result.Success();
    }
}
