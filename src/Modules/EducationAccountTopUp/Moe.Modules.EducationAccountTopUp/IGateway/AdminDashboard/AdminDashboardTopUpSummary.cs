namespace Moe.Modules.EducationAccountTopUp.IGateway.AdminDashboard;

public sealed record AdminDashboardTopUpSummary(
    string CurrencyCode,
    decimal MonthlyTotalAmount,
    IReadOnlyCollection<AdminDashboardTopUpSeriesPoint> YearlySeries);

public sealed record AdminDashboardTopUpSeriesPoint(
    int Month,
    decimal Amount);
