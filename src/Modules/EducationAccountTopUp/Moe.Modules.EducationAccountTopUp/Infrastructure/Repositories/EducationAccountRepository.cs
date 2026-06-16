using Microsoft.EntityFrameworkCore;
using Moe.Modules.EducationAccountTopUp.Domain.EducationAccounts;
using Moe.Modules.EducationAccountTopUp.IGateway.Repositories;
using Moe.StudentFinance.Persistence;

namespace Moe.Modules.EducationAccountTopUp.Infrastructure.Repositories;

internal sealed class EducationAccountRepository(MoeDbContext dbContext) : IEducationAccountRepository
{
    public Task<EducationAccount?> FindByPersonIdAsync(long personId, CancellationToken cancellationToken)
    {
        return dbContext.Set<EducationAccount>()
            .SingleOrDefaultAsync(x => x.PersonId == personId, cancellationToken);
    }

    public Task<bool> ExistsForPersonAsync(long personId, CancellationToken cancellationToken)
    {
        return dbContext.Set<EducationAccount>()
            .AnyAsync(x => x.PersonId == personId, cancellationToken);
    }

    public async Task AddAsync(EducationAccount account, CancellationToken cancellationToken)
    {
        await dbContext.Set<EducationAccount>().AddAsync(account, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
