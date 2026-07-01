namespace Moe.Modules.EducationAccountTopUp.IGateway.Dashboard;

public interface IAdminDashboardFinanceMetricsReader
{
    Task<AdminDashboardFinanceMetrics> GetHqMetricsAsync(
        int year,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    Task<AdminDashboardFinanceMetrics> GetSchoolMetricsAsync(
        long organizationId,
        int year,
        DateTimeOffset now,
        CancellationToken cancellationToken);
}

public sealed record AdminDashboardFinanceMetrics(
    long TotalActiveEducationAccounts,
    long NewEducationAccountsThisMonth,
    decimal TopUpAmountThisMonth,
    long ActiveTopUpCampaigns,
    string CurrencyCode,
    IReadOnlyCollection<AdminDashboardFinanceCountPoint> MonthlyNewEducationAccounts,
    IReadOnlyCollection<AdminDashboardFinanceAmountPoint> MonthlyTopUpAmounts);

public sealed record AdminDashboardFinanceCountPoint(int Month, long Value);

public sealed record AdminDashboardFinanceAmountPoint(int Month, decimal Amount);
