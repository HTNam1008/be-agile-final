using Moe.Modules.Mfa.Domain;

namespace Moe.Modules.Mfa.IGateway.Repositories;

internal interface IMfaCredentialRepository
{
    Task<bool> HasActivePinAsync(long loginAccountId, CancellationToken cancellationToken);

    Task<LoginMfaCredential?> FindPinAsync(long loginAccountId, CancellationToken cancellationToken);

    Task<LoginMfaCredential?> FindActivePinAsync(long loginAccountId, CancellationToken cancellationToken);

    void Add(LoginMfaCredential credential);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
