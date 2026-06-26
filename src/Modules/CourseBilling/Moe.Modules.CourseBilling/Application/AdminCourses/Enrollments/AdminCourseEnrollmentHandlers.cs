using Moe.Application.Abstractions.Messaging;
using Moe.Modules.CourseBilling.Contracts.AdminCourses;
using Moe.Modules.CourseBilling.Domain.Courses;
using Moe.Modules.CourseBilling.IGateway.Fas;
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
    IFasCourseSubsidyGateway fasSubsidies)
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
}
