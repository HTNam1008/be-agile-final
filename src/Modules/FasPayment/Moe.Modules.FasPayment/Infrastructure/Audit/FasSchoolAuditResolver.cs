using Microsoft.EntityFrameworkCore;
using Moe.Modules.CourseBilling.Domain.Courses;
using Moe.Modules.FasPayment.Application.Audit;
using Moe.Modules.FasPayment.Domain.Fas;
using Moe.StudentFinance.Persistence;

namespace Moe.Modules.FasPayment.Infrastructure.Audit;

internal sealed class FasSchoolAuditResolver(MoeDbContext dbContext) : IFasSchoolAuditResolver
{
    public async Task<IReadOnlyCollection<long>> ResolveFromCourseIdsAsync(
        IReadOnlyCollection<long> courseIds,
        CancellationToken cancellationToken)
    {
        long[] distinctCourseIds = courseIds.Distinct().ToArray();
        if (distinctCourseIds.Length == 0)
        {
            return Array.Empty<long>();
        }

        return await dbContext.Set<Course>()
            .AsNoTracking()
            .Where(course => distinctCourseIds.Contains(course.Id))
            .Select(course => course.OrganizationId)
            .Distinct()
            .ToArrayAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<long>> ResolveForSchemeAsync(
        long schemeId,
        CancellationToken cancellationToken)
    {
        return await (
                from schemeCourse in dbContext.Set<FasSchemeCourse>().AsNoTracking()
                join course in dbContext.Set<Course>().AsNoTracking()
                    on schemeCourse.CourseId equals course.Id
                where schemeCourse.FasSchemeId == schemeId
                select course.OrganizationId)
            .Distinct()
            .ToArrayAsync(cancellationToken);
    }
}
