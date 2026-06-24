using Moe.Modules.EducationAccountTopUp.Domain.Lifecycle;
using Moe.Modules.EducationAccountTopUp.IGateway.Repositories;
using Moe.StudentFinance.Persistence;

namespace Moe.Modules.EducationAccountTopUp.Infrastructure.Repositories;

internal sealed class EducationAccountLifecycleRunRepository(MoeDbContext dbContext)
    : IEducationAccountLifecycleRunRepository
{
    public async Task AddAsync(
        EducationAccountLifecycleRun run,
        CancellationToken cancellationToken)
    {
        await dbContext.Set<EducationAccountLifecycleRun>().AddAsync(run, cancellationToken);
    }
}
