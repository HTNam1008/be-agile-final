using Microsoft.EntityFrameworkCore;
using Moe.Modules.EducationAccountTopUp.Domain.TopUps;
using Moe.Modules.EducationAccountTopUp.IGateway.Repositories;
using Moe.StudentFinance.Persistence;

namespace Moe.Modules.EducationAccountTopUp.Infrastructure.Repositories;

internal sealed class TopUpTransactionRepository(MoeDbContext dbContext) : ITopUpTransactionRepository
{
    public Task<TopUpTransaction?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        return dbContext.Set<TopUpTransaction>()
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public Task<TopUpTransaction?> GetByIdempotencyKeyAsync(
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        return dbContext.Set<TopUpTransaction>()
            .SingleOrDefaultAsync(x => x.IdempotencyKey == idempotencyKey, cancellationToken);
    }

    public Task<TopUpTransaction?> GetByRunAndAccountAsync(
        long topUpRunId,
        long educationAccountId,
        CancellationToken cancellationToken = default)
    {
        return dbContext.Set<TopUpTransaction>()
            .SingleOrDefaultAsync(
                x => x.TopUpRunId == topUpRunId && x.EducationAccountId == educationAccountId,
                cancellationToken);
    }

    public Task<List<TopUpTransaction>> GetByRunIdAsync(
        long topUpRunId,
        CancellationToken cancellationToken = default)
    {
        return dbContext.Set<TopUpTransaction>()
            .Where(x => x.TopUpRunId == topUpRunId)
            .ToListAsync(cancellationToken);
    }

    public void Add(TopUpTransaction transaction)
    {
        dbContext.Set<TopUpTransaction>().Add(transaction);
    }

    public async Task AddAsync(TopUpTransaction transaction, CancellationToken cancellationToken = default)
    {
        await dbContext.Set<TopUpTransaction>().AddAsync(transaction, cancellationToken);
    }
}
