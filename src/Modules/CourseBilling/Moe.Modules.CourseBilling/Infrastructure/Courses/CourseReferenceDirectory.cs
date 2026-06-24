using Microsoft.EntityFrameworkCore;
using Moe.Modules.CourseBilling.Domain.Courses;
using Moe.Modules.CourseBilling.IGateway.Courses;
using Moe.StudentFinance.Persistence;

namespace Moe.Modules.CourseBilling.Infrastructure.Courses;

internal sealed class CourseReferenceDirectory(MoeDbContext dbContext) : ICourseReferenceDirectory
{
    public async Task<IReadOnlyList<long>> FindUnknownCourseIdsAsync(
        IReadOnlyCollection<long> courseIds,
        CancellationToken cancellationToken)
    {
        long[] requested = courseIds.Distinct().ToArray();
        if (requested.Length == 0)
        {
            return [];
        }

        long[] existing = await dbContext.Set<Course>()
            .AsNoTracking()
            .Where(course => requested.Contains(course.Id))
            .Select(course => course.Id)
            .ToArrayAsync(cancellationToken);

        return requested.Except(existing).Order().ToArray();
    }
}
