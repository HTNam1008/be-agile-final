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
    IAdminDashboardCourseMetricsReader courses)
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

        AdminDashboardIdentityMetrics identity = await identities.GetSchoolMetricsAsync(organizationId, year, now, cancellationToken);
        AdminDashboardSchoolFinanceMetrics financeMetrics = await finance.GetSchoolMetricsAsync(organizationId, year, now, cancellationToken);
        long totalCourses = await courses.CountTotalCoursesAsync(organizationId, cancellationToken);
        Dictionary<int, long?> studentsByMonth = identity.MonthlyActiveStudents.ToDictionary(point => point.Month, point => point.Value);
        Dictionary<int, decimal> topUpsByMonth = financeMetrics.MonthlyTopUpAmounts.ToDictionary(point => point.Month, point => point.Amount);
        SchoolDashboardMonthlyMetricsPoint[] points = Enumerable.Range(1, 12)
            .Select(month => new SchoolDashboardMonthlyMetricsPoint(
                month,
                studentsByMonth.GetValueOrDefault(month),
                IsFutureMonth(year, month, now) ? null : topUpsByMonth.GetValueOrDefault(month)))
            .ToArray();

        return Result<SchoolDashboardResponse>.Success(new SchoolDashboardResponse(
            new SchoolDashboardCardsResponse(
                new DashboardCountMetricResponse(
                    identity.TotalStudents,
                    DashboardTrend.Calculate(identity.TotalStudents, identity.PreviousPeriodTotalStudents)),
                new DashboardCountMetricResponse(
                    totalCourses,
                    null),
                new DashboardAmountMetricResponse(
                    financeMetrics.TopUpAmountThisMonth,
                    financeMetrics.CurrencyCode,
                    DashboardTrend.Calculate(
                        financeMetrics.TopUpAmountThisMonth,
                        financeMetrics.PreviousPeriodTopUpAmount))),
            new SchoolDashboardYearlyMetricsResponse(year, financeMetrics.CurrencyCode, points)));
    }

    private static bool IsFutureMonth(int year, int month, DateTimeOffset now)
        => year > now.Year || (year == now.Year && month > now.Month);
}
