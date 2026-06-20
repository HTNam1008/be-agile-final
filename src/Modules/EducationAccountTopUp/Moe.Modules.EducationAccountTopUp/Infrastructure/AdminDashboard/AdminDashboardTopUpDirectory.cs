using Microsoft.EntityFrameworkCore;
using Moe.Modules.EducationAccountTopUp.Domain.EducationAccounts;
using Moe.Modules.EducationAccountTopUp.Domain.TopUps;
using Moe.Modules.EducationAccountTopUp.IGateway.AdminDashboard;
using Moe.StudentFinance.Persistence;

namespace Moe.Modules.EducationAccountTopUp.Infrastructure.AdminDashboard;

internal sealed class AdminDashboardTopUpDirectory(MoeDbContext dbContext) : IAdminDashboardTopUpDirectory
{
    public async Task<AdminDashboardTopUpSummary> GetSummaryAsync(
        long? organizationId,
        int year,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        DateTime yearStart = new(year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        DateTime yearEnd = yearStart.AddYears(1);
        DateTime monthStart = new(utcNow.Year, utcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        DateTime monthEnd = monthStart.AddMonths(1);

        IQueryable<TopUpRun> completedRuns =
            from run in dbContext.Set<TopUpRun>().AsNoTracking()
            join campaign in dbContext.Set<TopUpCampaign>().AsNoTracking()
                on run.TopUpCampaignId equals campaign.Id
            where (organizationId == null || campaign.OrganizationId == organizationId)
                && run.CompletedAtUtc != null
                && (run.RunStatusCode == TopUpRunStatusCodes.Completed
                    || run.RunStatusCode == TopUpRunStatusCodes.Partial)
            select run;

        decimal monthlyTotal = await completedRuns
            .Where(run => run.CompletedAtUtc >= monthStart && run.CompletedAtUtc < monthEnd)
            .SumAsync(run => (decimal?)run.TotalAmount, cancellationToken) ?? 0m;

        var yearlyRows = await completedRuns
            .Where(run => run.CompletedAtUtc >= yearStart && run.CompletedAtUtc < yearEnd)
            .GroupBy(run => run.CompletedAtUtc!.Value.Month)
            .Select(group => new
            {
                Month = group.Key,
                Amount = group.Sum(run => run.TotalAmount)
            })
            .ToListAsync(cancellationToken);

        Dictionary<int, decimal> amountsByMonth = yearlyRows.ToDictionary(x => x.Month, x => x.Amount);

        AdminDashboardTopUpSeriesPoint[] series = Enumerable.Range(1, 12)
            .Select(month => new AdminDashboardTopUpSeriesPoint(
                month,
                amountsByMonth.GetValueOrDefault(month)))
            .ToArray();

        return new AdminDashboardTopUpSummary(
            CurrencyCodes.SingaporeDollar,
            monthlyTotal,
            series);
    }
}
