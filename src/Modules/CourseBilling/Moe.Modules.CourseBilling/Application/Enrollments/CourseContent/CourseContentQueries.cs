using Moe.Application.Abstractions.Messaging;
using Moe.Modules.CourseBilling.Contracts.Enrollments;

namespace Moe.Modules.CourseBilling.Application.Enrollments.CourseContent;

public sealed record GetStudentCourseContentQuery(long EnrollmentId)
    : IQuery<StudentCourseContentResponse>;

public sealed record DownloadStudentCourseMaterialQuery(long EnrollmentId, long CourseMaterialId)
    : IQuery<StudentCourseMaterialDownload>;

public sealed record StudentCourseMaterialDownload(
    Stream Content,
    string ContentType,
    string FileName);
