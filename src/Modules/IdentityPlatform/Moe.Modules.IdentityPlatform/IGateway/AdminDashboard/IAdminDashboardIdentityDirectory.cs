namespace Moe.Modules.IdentityPlatform.IGateway.AdminDashboard;

public interface IAdminDashboardIdentityDirectory
{
    Task<AdminDashboardIdentitySummary?> GetSummaryAsync(
        long userAccountId,
        long? organizationId,
        CancellationToken cancellationToken);
}
