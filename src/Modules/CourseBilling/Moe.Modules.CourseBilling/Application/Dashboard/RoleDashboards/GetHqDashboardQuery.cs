using Moe.Application.Abstractions.Messaging;

namespace Moe.Modules.CourseBilling.Application.Dashboard.RoleDashboards;

public sealed record GetHqDashboardQuery(int? Year) : IQuery<HqDashboardResponse>;

public sealed record HqDashboardResponse(
    HqDashboardCardsResponse Cards,
    HqDashboardYearlyGrowthResponse YearlyGrowth);

public sealed record HqDashboardCardsResponse(
    DashboardCountMetricResponse TotalSchools,
    DashboardCountMetricResponse TotalStudents,
    DashboardCountMetricResponse TotalEducationAccounts);

public sealed record HqDashboardYearlyGrowthResponse(
    int Year,
    IReadOnlyCollection<HqDashboardMonthlyGrowthPoint> Points);

public sealed record HqDashboardMonthlyGrowthPoint(
    int Month,
    long NewStudents,
    long NewEducationAccounts);
