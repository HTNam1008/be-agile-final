using Microsoft.EntityFrameworkCore;
using Moe.Modules.IdentityPlatform.Domain.Iam;
using Moe.Modules.IdentityPlatform.Domain.People;
using Moe.Modules.IdentityPlatform.IGateway.Repositories;
using Moe.StudentFinance.Persistence;

namespace Moe.Modules.IdentityPlatform.Infrastructure.Repositories;

internal sealed class LocalIdentityRepository(MoeDbContext dbContext) : ILocalIdentityRepository
{
    public Task<UserAccount?> FindUserAccountAsync(long userAccountId, CancellationToken cancellationToken)
    {
        return dbContext.Set<UserAccount>()
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == userAccountId, cancellationToken);
    }

    public Task<Person?> FindPersonAsync(long personId, CancellationToken cancellationToken)
    {
        return dbContext.Set<Person>()
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == personId, cancellationToken);
    }
}
