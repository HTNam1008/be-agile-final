using Microsoft.EntityFrameworkCore;
using Moe.Modules.CourseBilling.IGateway.Dashboard;
using Moe.Modules.FasPayment.Domain.Fas;
using Moe.StudentFinance.Persistence;

namespace Moe.Modules.FasPayment.Infrastructure.Dashboard;

internal sealed class AdminDashboardFasMetricsReader(MoeDbContext dbContext)
    : IAdminDashboardFasMetricsReader
{
    public Task<long> CountPendingApplicationsAsync(
        long? organizationId,
        CancellationToken cancellationToken)
        => dbContext.Set<FasApplication>()
            .AsNoTracking()
            .LongCountAsync(application =>
                (organizationId == null || application.SchoolOrganizationId == organizationId)
                && (application.StatusCode == FasApplicationStatuses.Submitted
                    || application.StatusCode == FasApplicationStatuses.PendingReview),
                cancellationToken);
}
