using Moe.Application.Abstractions.Clock;
using Moe.Application.Abstractions.Messaging;
using Moe.Application.Abstractions.Security;
using Moe.Modules.CourseBilling.IGateway.Dashboard;
using Moe.Modules.EducationAccountTopUp.IGateway.Dashboard;
using Moe.Modules.IdentityPlatform.IGateway.Dashboard;
using Moe.SharedKernel.Results;

namespace Moe.Modules.CourseBilling.Application.Dashboard.RoleDashboards;

internal sealed class GetHqDashboardHandler(
    IAdminAccessControl adminAccess,
    IClock clock,
    IAdminDashboardIdentityMetricsReader identities,
    IAdminDashboardFinanceMetricsReader finance)
    : IQueryHandler<GetHqDashboardQuery, HqDashboardResponse>
{
    public async Task<Result<HqDashboardResponse>> Handle(
        GetHqDashboardQuery query,
        CancellationToken cancellationToken)
    {
        if (!adminAccess.IsHqAdmin)
        {
            return Result<HqDashboardResponse>.Failure(RoleDashboardErrors.HqAdminRequired);
        }

        DateTimeOffset now = clock.UtcNow;
        int year = query.Year ?? now.Year;
        if (year is < 2000 or > 2100)
        {
            return Result<HqDashboardResponse>.Failure(RoleDashboardErrors.InvalidYear);
        }

        AdminDashboardIdentityMetrics identity = await identities.GetHqMetricsAsync(year, now, cancellationToken);
        AdminDashboardHqFinanceMetrics financeMetrics = await finance.GetHqMetricsAsync(year, now, cancellationToken);
        Dictionary<int, long> studentsByMonth = identity.MonthlyNewStudents.ToDictionary(point => point.Month, point => point.Value);
        Dictionary<int, long> accountsByMonth = financeMetrics.MonthlyNewEducationAccounts.ToDictionary(point => point.Month, point => point.Value);

        HqDashboardMonthlyGrowthPoint[] points = Enumerable.Range(1, 12)
            .Select(month => new HqDashboardMonthlyGrowthPoint(
                month,
                studentsByMonth.GetValueOrDefault(month),
                accountsByMonth.GetValueOrDefault(month)))
            .ToArray();

        return Result<HqDashboardResponse>.Success(new HqDashboardResponse(
            new HqDashboardCardsResponse(
                new DashboardCountMetricResponse(
                    identity.TotalSchools,
                    DashboardTrend.Calculate(identity.TotalSchools, identity.PreviousPeriodTotalSchools)),
                new DashboardCountMetricResponse(
                    identity.TotalStudents,
                    DashboardTrend.Calculate(identity.TotalStudents, identity.PreviousPeriodTotalStudents)),
                new DashboardCountMetricResponse(
                    financeMetrics.TotalEducationAccounts,
                    DashboardTrend.Calculate(
                        financeMetrics.TotalEducationAccounts,
                        financeMetrics.PreviousPeriodTotalEducationAccounts))),
            new HqDashboardYearlyGrowthResponse(year, points)));
    }
}
