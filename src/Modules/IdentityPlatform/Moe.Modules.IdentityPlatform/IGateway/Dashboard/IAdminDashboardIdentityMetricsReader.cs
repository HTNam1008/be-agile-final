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
    long TotalSchools,
    long PreviousPeriodTotalSchools,
    long TotalStudents,
    long PreviousPeriodTotalStudents,
    IReadOnlyCollection<AdminDashboardCountPoint> MonthlyNewStudents,
    IReadOnlyCollection<AdminDashboardNullableCountPoint> MonthlyActiveStudents);

public sealed record AdminDashboardCountPoint(int Month, long Value);

public sealed record AdminDashboardNullableCountPoint(int Month, long? Value);
