using Moe.Modules.IdentityPlatform.Domain.Iam;

namespace Moe.Modules.IdentityPlatform.IGateway.Repositories;

internal interface IIdentityProvisioningRequestRepository
{
    Task<IdentityProvisioningRequest?> FindByIdAsync(long requestId, CancellationToken cancellationToken);

    Task<IdentityProvisioningRequest?> FindByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken);

    Task AddAsync(IdentityProvisioningRequest request, CancellationToken cancellationToken);
}
