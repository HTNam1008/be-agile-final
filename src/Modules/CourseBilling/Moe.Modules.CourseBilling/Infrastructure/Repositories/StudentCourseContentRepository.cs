using Microsoft.EntityFrameworkCore;
using Moe.Modules.CourseBilling.Domain.Courses;
using Moe.Modules.CourseBilling.IGateway.Repositories;
using Moe.StudentFinance.Persistence;

namespace Moe.Modules.CourseBilling.Infrastructure.Repositories;

internal sealed class StudentCourseContentRepository(MoeDbContext dbContext)
    : IStudentCourseContentRepository
{
    public async Task<StudentCourseContentSnapshot?> FindAsync(
        long enrollmentId,
        long personId,
        CancellationToken cancellationToken)
    {
        CourseEnrollment? enrollment = await dbContext.Set<CourseEnrollment>()
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.Id == enrollmentId && x.PersonId == personId,
                cancellationToken);
        if (enrollment is null)
            return null;

        Course? course = await dbContext.Set<Course>()
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == enrollment.CourseId, cancellationToken);
        if (course is null)
            return null;

        CourseMaterial[] materials = await dbContext.Set<CourseMaterial>()
            .AsNoTracking()
            .Where(x => x.CourseId == course.Id && x.IsActive)
            .OrderBy(x => x.DisplayOrder)
            .ThenBy(x => x.Id)
            .ToArrayAsync(cancellationToken);

        return new StudentCourseContentSnapshot(enrollment, course, materials);
    }
}
