namespace Moe.Modules.IdentityPlatform.IGateway.Dashboard;

public interface IAdminDashboardIdentityMetricsReader
{
    Task<AdminDashboardIdentityMetrics> GetHqMetricsAsync(
        int year,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    Task<AdminDashboardIdentityMetrics> GetSchoolMetricsAsync(
        long organizationId,
        int year,
        DateTimeOffset now,
        CancellationToken cancellationToken);
}

public sealed record AdminDashboardIdentityMetrics(
    long TotalActiveSchools,
    long TotalActiveStudents,
    long NewStudentsThisMonth,
    IReadOnlyCollection<AdminDashboardCountPoint> MonthlyNewStudents);

public sealed record AdminDashboardCountPoint(int Month, long Value);
