using Moe.Application.Abstractions.Clock;
using Moe.Application.Abstractions.Messaging;
using Moe.Application.Abstractions.Security;
using Moe.Modules.CourseBilling.IGateway.Dashboard;
using Moe.Modules.EducationAccountTopUp.IGateway.Dashboard;
using Moe.Modules.IdentityPlatform.IGateway.Dashboard;
using Moe.SharedKernel.Results;

namespace Moe.Modules.CourseBilling.Application.Dashboard.RoleDashboards;

internal sealed class GetSchoolDashboardHandler(
    IAdminAccessControl adminAccess,
    IClock clock,
    IAdminDashboardIdentityMetricsReader identities,
    IAdminDashboardFinanceMetricsReader finance,
    IAdminDashboardCourseMetricsReader courses,
    IAdminDashboardFasMetricsReader fas)
    : IQueryHandler<GetSchoolDashboardQuery, SchoolDashboardResponse>
{
    public async Task<Result<SchoolDashboardResponse>> Handle(
        GetSchoolDashboardQuery query,
        CancellationToken cancellationToken)
    {
        if (!adminAccess.IsSchoolAdmin)
        {
            return Result<SchoolDashboardResponse>.Failure(RoleDashboardErrors.SchoolAdminRequired);
        }

        AdminOrganizationScope scope = adminAccess.ResolveOrganizationFilter(null);
        if (!scope.HasAccess || scope.OrganizationId is not long organizationId)
        {
            return Result<SchoolDashboardResponse>.Failure(RoleDashboardErrors.SchoolScopeRequired);
        }

        DateTimeOffset now = clock.UtcNow;
        int year = query.Year ?? now.Year;
        if (year is < 2000 or > 2100)
        {
            return Result<SchoolDashboardResponse>.Failure(RoleDashboardErrors.InvalidYear);
        }

        DateOnly currentDate = clock.TodayInSingapore();
        AdminDashboardIdentityMetrics identity = await identities.GetSchoolMetricsAsync(organizationId, year, now, cancellationToken);
        AdminDashboardFinanceMetrics financeMetrics = await finance.GetSchoolMetricsAsync(organizationId, year, now, cancellationToken);
        long activeCourses = await courses.CountActiveCoursesAsync(organizationId, currentDate, cancellationToken);
        long activeEnrollments = await courses.CountActiveEnrollmentsAsync(organizationId, currentDate, cancellationToken);
        long pendingFasApplications = await fas.CountPendingApplicationsAsync(organizationId, cancellationToken);
        Dictionary<int, decimal> topUpsByMonth = financeMetrics.MonthlyTopUpAmounts.ToDictionary(point => point.Month, point => point.Amount);
        SchoolDashboardMonthlyTopUpPoint[] points = Enumerable.Range(1, 12)
            .Select(month => new SchoolDashboardMonthlyTopUpPoint(month, topUpsByMonth.GetValueOrDefault(month)))
            .ToArray();

        return Result<SchoolDashboardResponse>.Success(new SchoolDashboardResponse(
            new SchoolDashboardCardsResponse(
                identity.TotalActiveStudents,
                activeCourses,
                financeMetrics.TotalActiveEducationAccounts,
                financeMetrics.TopUpAmountThisMonth,
                financeMetrics.CurrencyCode),
            new SchoolDashboardTopUpYearlyResponse(year, financeMetrics.CurrencyCode, points),
            new SchoolDashboardOverviewResponse(
                identity.NewStudentsThisMonth,
                activeEnrollments,
                pendingFasApplications,
                financeMetrics.ActiveTopUpCampaigns)));
    }
}
