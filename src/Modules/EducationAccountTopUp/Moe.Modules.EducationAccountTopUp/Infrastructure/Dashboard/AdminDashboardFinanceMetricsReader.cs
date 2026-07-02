using Microsoft.EntityFrameworkCore;
using Moe.Modules.EducationAccountTopUp.Domain.EducationAccounts;
using Moe.Modules.EducationAccountTopUp.Domain.TopUps;
using Moe.Modules.EducationAccountTopUp.IGateway.Dashboard;
using Moe.StudentFinance.Persistence;

namespace Moe.Modules.EducationAccountTopUp.Infrastructure.Dashboard;

internal sealed class AdminDashboardFinanceMetricsReader(MoeDbContext dbContext)
    : IAdminDashboardFinanceMetricsReader
{
    public async Task<AdminDashboardHqFinanceMetrics> GetHqMetricsAsync(
        int year,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        DateTimeOffset previousPeriodCutoff = PreviousPeriodCutoff(now);
        IQueryable<EducationAccount> accounts = dbContext.Set<EducationAccount>().AsNoTracking();

        long totalAccounts = await accounts
            .LongCountAsync(account => account.OpenedAtUtc <= now, cancellationToken);
        long previousPeriodTotalAccounts = await accounts
            .LongCountAsync(account => account.OpenedAtUtc <= previousPeriodCutoff, cancellationToken);

        DateTimeOffset yearStart = new(year, 1, 1, 0, 0, 0, TimeSpan.Zero);
        DateTimeOffset yearEnd = yearStart.AddYears(1);
        var rows = await accounts
            .Where(account => account.OpenedAtUtc >= yearStart && account.OpenedAtUtc < yearEnd)
            .GroupBy(account => account.OpenedAtUtc.Month)
            .Select(group => new { Month = group.Key, Value = group.LongCount() })
            .ToListAsync(cancellationToken);

        return new AdminDashboardHqFinanceMetrics(
            totalAccounts,
            previousPeriodTotalAccounts,
            rows.Select(row => new AdminDashboardFinanceCountPoint(row.Month, row.Value)).ToArray());
    }

    public async Task<AdminDashboardSchoolFinanceMetrics> GetSchoolMetricsAsync(
        long organizationId,
        int year,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        DateTimeOffset singaporeNow = now.ToUniversalTime().ToOffset(TimeSpan.FromHours(8));
        DateTimeOffset monthStart = new(
            singaporeNow.Year,
            singaporeNow.Month,
            1,
            0,
            0,
            0,
            singaporeNow.Offset);
        DateTimeOffset previousMonthStart = monthStart.AddMonths(-1);
        DateTimeOffset previousPeriodCutoff = PreviousPeriodCutoff(singaporeNow);

        IQueryable<TopUpRun> completedRuns =
            from run in dbContext.Set<TopUpRun>().AsNoTracking()
            join campaign in dbContext.Set<TopUpCampaign>().AsNoTracking()
                on run.TopUpCampaignId equals campaign.Id
            where campaign.OrganizationId == organizationId
                && run.CompletedAtUtc != null
                && (run.RunStatusCode == TopUpRunStatusCodes.Completed
                    || run.RunStatusCode == TopUpRunStatusCodes.Partial)
            select run;

        decimal currentAmount = await completedRuns
            .Where(run => run.CompletedAtUtc >= monthStart.UtcDateTime
                && run.CompletedAtUtc <= now.UtcDateTime)
            .SumAsync(run => (decimal?)run.TotalAmount, cancellationToken) ?? 0m;
        decimal previousAmount = await completedRuns
            .Where(run => run.CompletedAtUtc >= previousMonthStart.UtcDateTime
                && run.CompletedAtUtc <= previousPeriodCutoff.UtcDateTime)
            .SumAsync(run => (decimal?)run.TotalAmount, cancellationToken) ?? 0m;

        DateTime yearStartUtc = new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        DateTime yearEndUtc = yearStartUtc.AddYears(1);
        var rows = await completedRuns
            .Where(run => run.CompletedAtUtc >= yearStartUtc && run.CompletedAtUtc < yearEndUtc)
            .GroupBy(run => run.CompletedAtUtc!.Value.Month)
            .Select(group => new { Month = group.Key, Amount = group.Sum(run => run.TotalAmount) })
            .ToListAsync(cancellationToken);

        return new AdminDashboardSchoolFinanceMetrics(
            currentAmount,
            previousAmount,
            CurrencyCodes.SingaporeDollar,
            rows.Select(row => new AdminDashboardFinanceAmountPoint(row.Month, row.Amount)).ToArray());
    }

    private static DateTimeOffset PreviousPeriodCutoff(DateTimeOffset now)
    {
        DateTimeOffset singaporeNow = now.ToUniversalTime().ToOffset(TimeSpan.FromHours(8));
        DateTimeOffset previousMonth = singaporeNow.AddMonths(-1);
        int day = Math.Min(singaporeNow.Day, DateTime.DaysInMonth(previousMonth.Year, previousMonth.Month));
        DateTime localCutoff = new(previousMonth.Year, previousMonth.Month, day);
        localCutoff = localCutoff.Add(singaporeNow.TimeOfDay);
        return new DateTimeOffset(localCutoff, singaporeNow.Offset).ToUniversalTime();
    }
}
