using Moe.Infrastructure.Shared.Api;

namespace Moe.Modules.CourseBilling.IGateway.AdminStudentCourses;

internal interface IAdminStudentEnrolledCourseReader
{
    Task<PageResponse<AdminStudentEnrolledCourseProjection>> ListAsync(
        long personId,
        int page,
        int pageSize,
        CancellationToken cancellationToken);
}

internal sealed record AdminStudentEnrolledCourseProjection(
    long CourseEnrollmentId,
    long CourseId,
    string CourseName,
    string EnrollmentStatusCode,
    DateTime EnrolledAtUtc,
    decimal Fee,
    decimal FasApplied,
    decimal Paid,
    decimal Outstanding);
