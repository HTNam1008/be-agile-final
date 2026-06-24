using Microsoft.EntityFrameworkCore;
using Moe.Modules.IdentityPlatform.Domain.People;
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

        return await dbContext.Set<Person>()
            .AsNoTracking()
            .Where(x => personIds.Contains(x.Id))
            .Select(x => new LifecyclePersonDisplaySummary(
                x.Id,
                x.OfficialFullName,
                x.IdentityNumberMasked ?? string.Empty))
            .ToArrayAsync(cancellationToken);
    }
}
