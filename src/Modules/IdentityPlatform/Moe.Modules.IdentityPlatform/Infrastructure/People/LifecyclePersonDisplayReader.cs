using Microsoft.EntityFrameworkCore;
using Moe.Modules.IdentityPlatform.Domain.Iam;
using Moe.Modules.IdentityPlatform.Domain.People;
using Moe.Modules.IdentityPlatform.Domain.Schooling;
using Moe.Modules.IdentityPlatform.IGateway.People;
using Moe.StudentFinance.Persistence;

namespace Moe.Modules.IdentityPlatform.Infrastructure.People;

internal sealed class LifecyclePersonDisplayReader(MoeDbContext dbContext)
    : ILifecyclePersonDisplayReader
{
    public async Task<IReadOnlyCollection<LifecyclePersonDisplaySummary>> FindByPersonIdsAsync(
        IReadOnlyCollection<long> personIds,
        CancellationToken cancellationToken)
    {
        if (personIds.Count == 0)
        {
            return [];
        }

        return await (
                from person in dbContext.Set<Person>().AsNoTracking()
                where personIds.Contains(person.Id)
                join enrollment in dbContext.Set<SchoolEnrollment>().AsNoTracking()
                    on person.Id equals enrollment.PersonId into enrollments
                from enrollment in enrollments
                    .Where(x => x.SchoolingStatusCode == "ACTIVE")
                    .OrderByDescending(x => x.StartDate)
                    .Take(1)
                    .DefaultIfEmpty()
                join organization in dbContext.Set<OrganizationUnit>().AsNoTracking()
                    on enrollment.OrganizationId equals organization.Id into organizations
                from organization in organizations.DefaultIfEmpty()
                select new LifecyclePersonDisplaySummary(
                    person.Id,
                    person.OfficialFullName,
                    person.IdentityNumberMasked ?? string.Empty,
                    organization == null ? null : organization.UnitName))
            .ToArrayAsync(cancellationToken);
    }
}
