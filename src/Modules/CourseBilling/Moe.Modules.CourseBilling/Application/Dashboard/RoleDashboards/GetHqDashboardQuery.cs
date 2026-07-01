using Moe.Application.Abstractions.Messaging;

namespace Moe.Modules.CourseBilling.Application.Dashboard.RoleDashboards;

public sealed record GetHqDashboardQuery(int? Year) : IQuery<HqDashboardResponse>;

public sealed record HqDashboardResponse(
    HqDashboardCardsResponse Cards,
    HqDashboardYearlyGrowthResponse YearlyGrowth,
    HqDashboardOverviewResponse Overview);

public sealed record HqDashboardCardsResponse(
    long TotalActiveSchools,
    long TotalActiveStudents,
    long TotalActiveEducationAccounts,
    long ActiveCourses);

public sealed record HqDashboardYearlyGrowthResponse(
    int Year,
    IReadOnlyCollection<HqDashboardMonthlyGrowthPoint> Points);

public sealed record HqDashboardMonthlyGrowthPoint(
    int Month,
    long NewStudents,
    long NewEducationAccounts);

public sealed record HqDashboardOverviewResponse(
    long NewStudentsThisMonth,
    long NewEducationAccountsThisMonth,
    long PendingFasApplications,
    decimal TopUpAmountThisMonth,
    string CurrencyCode);
