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

    public Task<bool> ExistsForScheduledOccurrenceAsync(
        long campaignId,
        DateTime scheduledFor,
        CancellationToken cancellationToken = default)
    {
        return dbContext.Set<TopUpRun>()
            .AnyAsync(
                x => x.TopUpCampaignId == campaignId && x.ScheduledForUtc == scheduledFor,
                cancellationToken);
    }

    public async Task AddAsync(TopUpRun run, CancellationToken cancellationToken = default)
    {
        await dbContext.Set<TopUpRun>().AddAsync(run, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
