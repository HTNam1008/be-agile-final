namespace Moe.Modules.CourseBilling.IGateway.Dashboard;

public interface IAdminDashboardFasMetricsReader
{
    Task<long> CountPendingApplicationsAsync(long? organizationId, CancellationToken cancellationToken);
}
