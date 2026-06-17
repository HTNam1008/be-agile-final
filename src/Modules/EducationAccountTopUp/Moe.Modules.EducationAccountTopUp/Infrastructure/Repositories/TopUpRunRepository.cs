using Microsoft.EntityFrameworkCore;
using Moe.Modules.EducationAccountTopUp.Domain.TopUps;
using Moe.Modules.EducationAccountTopUp.IGateway.Repositories;
using Moe.StudentFinance.Persistence;

namespace Moe.Modules.EducationAccountTopUp.Infrastructure.Repositories;

internal sealed class TopUpRunRepository(MoeDbContext dbContext) : ITopUpRunRepository
{
    public Task<TopUpRun?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        return dbContext.Set<TopUpRun>().SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public Task<TopUpRun?> GetByIdempotencyKeyAsync(
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        return dbContext.Set<TopUpRun>()
            .SingleOrDefaultAsync(x => x.IdempotencyKey == idempotencyKey, cancellationToken);
    }

    public void Add(TopUpRun run)
    {
        dbContext.Set<TopUpRun>().Add(run);
    }
}
