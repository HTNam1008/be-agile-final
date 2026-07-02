using Moe.Application.Abstractions.Messaging;

namespace Moe.Modules.CourseBilling.Application.Dashboard.RoleDashboards;

public sealed record GetSchoolDashboardQuery(int? Year) : IQuery<SchoolDashboardResponse>;

public sealed record SchoolDashboardResponse(
    SchoolDashboardCardsResponse Cards,
    SchoolDashboardYearlyMetricsResponse YearlyMetrics);

public sealed record SchoolDashboardCardsResponse(
    DashboardCountMetricResponse TotalStudents,
    DashboardCountMetricResponse TotalCourses,
    DashboardAmountMetricResponse TopUpAmountThisMonth);

public sealed record SchoolDashboardYearlyMetricsResponse(
    int Year,
    string CurrencyCode,
    IReadOnlyCollection<SchoolDashboardMonthlyMetricsPoint> Points);

public sealed record SchoolDashboardMonthlyMetricsPoint(
    int Month,
    long? ActiveStudents,
    decimal? TopUpAmount);
