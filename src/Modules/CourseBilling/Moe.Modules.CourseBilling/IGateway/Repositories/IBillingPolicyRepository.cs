using Moe.Modules.CourseBilling.Domain.Billing;

namespace Moe.Modules.CourseBilling.IGateway.Repositories;

internal interface IBillingPolicyRepository
{
    Task<OrganizationBillingConfiguration?> FindConfigurationAsync(
        long organizationId,
        CancellationToken cancellationToken);

    Task AddConfigurationAsync(
        OrganizationBillingConfiguration configuration,
        CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
