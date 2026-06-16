using Microsoft.EntityFrameworkCore;
using Moe.Modules.IdentityPlatform.Domain.Iam;
using Moe.Modules.IdentityPlatform.IGateway.Repositories;
using Moe.StudentFinance.Persistence;

namespace Moe.Modules.IdentityPlatform.Infrastructure.Repositories;

internal sealed class IdentityProvisioningRequestRepository(MoeDbContext dbContext) : IIdentityProvisioningRequestRepository
{
    public Task<IdentityProvisioningRequest?> FindByIdAsync(long requestId, CancellationToken cancellationToken)
    {
        return dbContext.Set<IdentityProvisioningRequest>()
            .SingleOrDefaultAsync(x => x.Id == requestId, cancellationToken);
    }

    public Task<IdentityProvisioningRequest?> FindByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken)
    {
        return dbContext.Set<IdentityProvisioningRequest>()
            .SingleOrDefaultAsync(x => x.IdempotencyKey == idempotencyKey, cancellationToken);
    }

    public async Task AddAsync(IdentityProvisioningRequest request, CancellationToken cancellationToken)
    {
        await dbContext.Set<IdentityProvisioningRequest>().AddAsync(request, cancellationToken);
    }
}
