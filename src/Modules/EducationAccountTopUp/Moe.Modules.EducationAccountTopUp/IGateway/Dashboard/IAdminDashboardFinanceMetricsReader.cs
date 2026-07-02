namespace Moe.Modules.EducationAccountTopUp.IGateway.Dashboard;

public interface IAdminDashboardFinanceMetricsReader
{
    Task<AdminDashboardHqFinanceMetrics> GetHqMetricsAsync(
        int year,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    Task<AdminDashboardSchoolFinanceMetrics> GetSchoolMetricsAsync(
        long organizationId,
        int year,
        DateTimeOffset now,
        CancellationToken cancellationToken);
}

public sealed record AdminDashboardHqFinanceMetrics(
    long TotalEducationAccounts,
    long PreviousPeriodTotalEducationAccounts,
    IReadOnlyCollection<AdminDashboardFinanceCountPoint> MonthlyNewEducationAccounts);

public sealed record AdminDashboardSchoolFinanceMetrics(
    decimal TopUpAmountThisMonth,
    decimal PreviousPeriodTopUpAmount,
    string CurrencyCode,
    IReadOnlyCollection<AdminDashboardFinanceAmountPoint> MonthlyTopUpAmounts);

public sealed record AdminDashboardFinanceCountPoint(int Month, long Value);

public sealed record AdminDashboardFinanceAmountPoint(int Month, decimal Amount);
