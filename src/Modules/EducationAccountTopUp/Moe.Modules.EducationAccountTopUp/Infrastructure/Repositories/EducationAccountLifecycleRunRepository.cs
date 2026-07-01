using Microsoft.EntityFrameworkCore;
using Moe.Modules.EducationAccountTopUp.Domain.Lifecycle;
using Moe.Modules.EducationAccountTopUp.IGateway.Repositories;
using Moe.StudentFinance.Persistence;

namespace Moe.Modules.EducationAccountTopUp.Infrastructure.Repositories;

internal sealed class EducationAccountLifecycleRunRepository(MoeDbContext dbContext)
    : IEducationAccountLifecycleRunRepository
{
    public async Task<bool> ScheduledRunExistsAsync(
        DateOnly runDateUtc,
        CancellationToken cancellationToken)
        => await dbContext
            .Set<EducationAccountLifecycleRun>()
            .AsNoTracking()
            .AnyAsync(
                x => x.RunDateUtc == runDateUtc
                     && x.TriggerTypeCode == EducationAccountLifecycleRunTriggerTypes.Scheduled,
                cancellationToken);

    public async Task AddAsync(
        EducationAccountLifecycleRun run,
        CancellationToken cancellationToken)
    {
        await dbContext.Set<EducationAccountLifecycleRun>().AddAsync(run, cancellationToken);
    }
}
