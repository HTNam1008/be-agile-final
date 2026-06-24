using Moe.Modules.Mfa.Domain;

namespace Moe.Modules.Mfa.IGateway.Repositories;

internal interface IMfaChallengeRepository
{
    Task<LoginMfaChallenge?> FindByIdAsync(Guid challengeId, CancellationToken cancellationToken);

    void Add(LoginMfaChallenge challenge);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
