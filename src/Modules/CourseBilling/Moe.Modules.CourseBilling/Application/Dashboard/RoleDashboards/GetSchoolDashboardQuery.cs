using Moe.Application.Abstractions.Messaging;

namespace Moe.Modules.CourseBilling.Application.Dashboard.RoleDashboards;

public sealed record GetSchoolDashboardQuery(int? Year) : IQuery<SchoolDashboardResponse>;

public sealed record SchoolDashboardResponse(
    SchoolDashboardCardsResponse Cards,
    SchoolDashboardTopUpYearlyResponse TopUpYearly,
    SchoolDashboardOverviewResponse Overview);

public sealed record SchoolDashboardCardsResponse(
    long TotalActiveStudents,
    long ActiveCourses,
    long TotalActiveEducationAccounts,
    decimal TopUpAmountThisMonth,
    string CurrencyCode);

public sealed record SchoolDashboardTopUpYearlyResponse(
    int Year,
    string CurrencyCode,
    IReadOnlyCollection<SchoolDashboardMonthlyTopUpPoint> Points);

public sealed record SchoolDashboardMonthlyTopUpPoint(int Month, decimal Amount);

public sealed record SchoolDashboardOverviewResponse(
    long NewStudentsThisMonth,
    long ActiveCourseEnrollments,
    long PendingFasApplications,
    long ActiveTopUpCampaigns);
