using Moe.Modules.CourseBilling.Contracts.BillingStatements;

namespace Moe.Modules.CourseBilling.IGateway.Repositories;

internal interface IBillingStatementRepository
{
    Task<BillingStatementResponse> GetOrCreateAsync(
        long personId,
        int year,
        int month,
        DateTime utcNow,
        CancellationToken cancellationToken);
}
