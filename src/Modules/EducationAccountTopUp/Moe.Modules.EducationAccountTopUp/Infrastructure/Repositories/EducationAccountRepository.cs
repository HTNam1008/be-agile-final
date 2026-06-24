using Microsoft.EntityFrameworkCore;
using Moe.Modules.EducationAccountTopUp.Domain.EducationAccounts;
using Moe.Modules.EducationAccountTopUp.IGateway.Repositories;
using Moe.StudentFinance.Persistence;

namespace Moe.Modules.EducationAccountTopUp.Infrastructure.Repositories;

internal sealed class EducationAccountRepository(MoeDbContext dbContext) : IEducationAccountRepository
{
    public Task<EducationAccount?> FindByIdAsync(long educationAccountId, CancellationToken cancellationToken)
    {
        return dbContext.Set<EducationAccount>()
            .SingleOrDefaultAsync(x => x.Id == educationAccountId, cancellationToken);
    }

    public Task<EducationAccount?> FindByPersonIdAsync(long personId, CancellationToken cancellationToken)
    {
        return dbContext.Set<EducationAccount>()
            .SingleOrDefaultAsync(x => x.PersonId == personId, cancellationToken);
    }

    public async Task<IReadOnlyCollection<EducationAccount>> ListActiveAsync(CancellationToken cancellationToken)
    {
        return await dbContext.Set<EducationAccount>()
            .Where(x => x.StatusCode == AccountStatuses.Active)
            .ToArrayAsync(cancellationToken);
    }

    public Task<bool> ExistsForPersonAsync(long personId, CancellationToken cancellationToken)
    {
        return dbContext.Set<EducationAccount>()
            .AnyAsync(x => x.PersonId == personId, cancellationToken);
    }

    public async Task AddAsync(EducationAccount account, CancellationToken cancellationToken)
    {
        await dbContext.Set<EducationAccount>().AddAsync(account, cancellationToken);
    }
}
