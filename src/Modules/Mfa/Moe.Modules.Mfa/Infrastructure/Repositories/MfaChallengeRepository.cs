using Microsoft.EntityFrameworkCore;
using Moe.Modules.Mfa.Domain;
using Moe.Modules.Mfa.IGateway.Repositories;
using Moe.StudentFinance.Persistence;

namespace Moe.Modules.Mfa.Infrastructure.Repositories;

internal sealed class MfaChallengeRepository(MoeDbContext dbContext) : IMfaChallengeRepository
{
    public Task<LoginMfaChallenge?> FindByIdAsync(Guid challengeId, CancellationToken cancellationToken)
    {
        return dbContext.Set<LoginMfaChallenge>()
            .SingleOrDefaultAsync(x => x.Id == challengeId, cancellationToken);
    }

    public void Add(LoginMfaChallenge challenge)
    {
        dbContext.Add(challenge);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        return dbContext.SaveChangesAsync(cancellationToken);
    }
}
