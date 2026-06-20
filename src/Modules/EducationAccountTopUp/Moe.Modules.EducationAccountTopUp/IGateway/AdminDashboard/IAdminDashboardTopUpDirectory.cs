namespace Moe.Modules.EducationAccountTopUp.IGateway.AdminDashboard;

public interface IAdminDashboardTopUpDirectory
{
    Task<AdminDashboardTopUpSummary> GetSummaryAsync(
        long? organizationId,
        int year,
        DateTime utcNow,
        CancellationToken cancellationToken);
}
