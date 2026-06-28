using Microsoft.EntityFrameworkCore;
using Moe.Modules.CourseBilling.Domain.Billing;
using Moe.Modules.CourseBilling.IGateway.Repositories;
using Moe.StudentFinance.Persistence;

namespace Moe.Modules.CourseBilling.Infrastructure.Repositories;

internal sealed class BillingPolicyRepository(MoeDbContext dbContext) : IBillingPolicyRepository
{
    public Task<OrganizationBillingConfiguration?> FindConfigurationAsync(
        long organizationId,
        CancellationToken cancellationToken)
        => dbContext.Set<OrganizationBillingConfiguration>()
            .SingleOrDefaultAsync(candidate => candidate.OrganizationId == organizationId, cancellationToken);

    public async Task AddConfigurationAsync(
        OrganizationBillingConfiguration configuration,
        CancellationToken cancellationToken)
        => await dbContext.Set<OrganizationBillingConfiguration>()
            .AddAsync(configuration, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken)
        => dbContext.SaveChangesAsync(cancellationToken);
}
