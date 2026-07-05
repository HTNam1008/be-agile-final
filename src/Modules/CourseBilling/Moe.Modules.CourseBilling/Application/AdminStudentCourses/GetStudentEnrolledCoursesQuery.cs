using Moe.Application.Abstractions.Messaging;
using Moe.Infrastructure.Shared.Api;

namespace Moe.Modules.CourseBilling.Application.AdminStudentCourses;

public sealed record GetStudentEnrolledCoursesQuery(
    long PersonId,
    int Page,
    int PageSize) : IQuery<PageResponse<AdminStudentEnrolledCourseItem>>;

public sealed record AdminStudentEnrolledCourseItem(
    long CourseEnrollmentId,
    long CourseId,
    string CourseName,
    string StatusLabel,
    DateTime EnrolledAtUtc,
    decimal Fee,
    decimal FasApplied,
    decimal Paid,
    decimal Outstanding);
