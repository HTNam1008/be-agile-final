using Moe.Modules.CourseBilling.Domain.Courses;

namespace Moe.Modules.CourseBilling.IGateway.Repositories;

internal sealed record StudentCourseContentSnapshot(
    CourseEnrollment Enrollment,
    Course Course,
    IReadOnlyCollection<CourseMaterial> Materials);

internal interface IStudentCourseContentRepository
{
    Task<StudentCourseContentSnapshot?> FindAsync(
        long enrollmentId,
        long personId,
        CancellationToken cancellationToken);
}
