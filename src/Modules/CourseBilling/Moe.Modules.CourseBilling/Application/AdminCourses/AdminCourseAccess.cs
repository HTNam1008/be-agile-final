using Moe.Application.Abstractions.Clock;
using Moe.Application.Abstractions.Security;
using Moe.Modules.CourseBilling.Domain.Courses;
using Moe.Modules.CourseBilling.IGateway.Repositories;
using Moe.Modules.CourseBilling.Infrastructure.Security;
using Moe.SharedKernel.Results;

namespace Moe.Modules.CourseBilling.Application.AdminCourses;

internal sealed class AdminCourseAccess(
    IAdminCourseRepository courses,
    ICurrentAdminContext currentAdmin,
    ICurrentUser currentUser,
    IClock clock)
{
    private const long MoeHeadquartersOrganizationId = 1;

    public IAdminCourseRepository Courses => courses;
    public ICurrentUser CurrentUser => currentUser;

    public DateTime UtcNow() => clock.UtcNow.UtcDateTime;

    public Result RequireAdmin()
        => currentAdmin.IsAdmin ? Result.Success() : Result.Failure(CourseErrors.AdminRequired);

    public IReadOnlyCollection<long>? OrganizationScope
        => HasGlobalOrganizationScope
            ? null
            : currentUser.OrganizationUnitIds;

    public bool CanAccessOrganization(long organizationId)
        => HasGlobalOrganizationScope
            || currentUser.OrganizationUnitIds.Contains(organizationId)
            || currentUser.OrganizationUnitId == organizationId;

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
        if (startDate > endDate)
        {
            return Result.Failure(CourseErrors.InvalidDateRange);
        }

        if (enrollmentOpenAt > enrollmentCloseAt)
        {
            return Result.Failure(CourseErrors.InvalidEnrollmentWindow);
        }

        if (DateOnly.FromDateTime(enrollmentCloseAt) > endDate)
        {
            return Result.Failure(CourseErrors.EnrollmentCloseAfterCourseEnd);
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

    private bool HasGlobalOrganizationScope
        => currentUser.HasPermission("ORG_VIEW_ALL")
            || currentUser.OrganizationUnitId == MoeHeadquartersOrganizationId
            || currentUser.OrganizationUnitIds.Contains(MoeHeadquartersOrganizationId);
}
