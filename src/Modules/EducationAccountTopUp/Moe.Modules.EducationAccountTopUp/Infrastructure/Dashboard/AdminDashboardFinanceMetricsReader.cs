using Microsoft.EntityFrameworkCore;
using Moe.Modules.EducationAccountTopUp.Domain.EducationAccounts;
using Moe.Modules.EducationAccountTopUp.Domain.TopUps;
using Moe.Modules.EducationAccountTopUp.IGateway.Dashboard;
using Moe.Modules.IdentityPlatform.Domain.Schooling;
using Moe.StudentFinance.Persistence;

namespace Moe.Modules.EducationAccountTopUp.Infrastructure.Dashboard;

internal sealed class AdminDashboardFinanceMetricsReader(MoeDbContext dbContext)
    : IAdminDashboardFinanceMetricsReader
{
    public Task<AdminDashboardFinanceMetrics> GetHqMetricsAsync(
        int year,
        DateTimeOffset now,
        CancellationToken cancellationToken)
        => GetMetricsAsync(null, year, now, cancellationToken);

    public Task<AdminDashboardFinanceMetrics> GetSchoolMetricsAsync(
        long organizationId,
        int year,
        DateTimeOffset now,
        CancellationToken cancellationToken)
        => GetMetricsAsync(organizationId, year, now, cancellationToken);

    private async Task<AdminDashboardFinanceMetrics> GetMetricsAsync(
        long? organizationId,
        int year,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        DateTime utcNow = now.UtcDateTime;
        DateTimeOffset yearStart = new(year, 1, 1, 0, 0, 0, TimeSpan.Zero);
        DateTimeOffset yearEnd = yearStart.AddYears(1);
        DateTimeOffset monthStart = new(utcNow.Year, utcNow.Month, 1, 0, 0, 0, TimeSpan.Zero);
        DateTimeOffset monthEnd = monthStart.AddMonths(1);

        IQueryable<EducationAccount> scopedAccounts = organizationId == null
            ? dbContext.Set<EducationAccount>().AsNoTracking()
            : from account in dbContext.Set<EducationAccount>().AsNoTracking()
              join enrollment in dbContext.Set<SchoolEnrollment>().AsNoTracking()
                  on account.PersonId equals enrollment.PersonId
              where enrollment.OrganizationId == organizationId
                  && enrollment.SchoolingStatusCode == "ACTIVE"
              select account;

        long totalActiveAccounts = await scopedAccounts
            .Where(account => account.StatusCode == AccountStatuses.Active)
            .Select(account => account.Id)
            .Distinct()
            .LongCountAsync(cancellationToken);

        long newAccountsThisMonth = organizationId == null
            ? await scopedAccounts
                .Where(account => account.OpenedAtUtc >= monthStart && account.OpenedAtUtc < monthEnd)
                .Select(account => account.Id)
                .Distinct()
                .LongCountAsync(cancellationToken)
            : 0;

        IReadOnlyCollection<AdminDashboardFinanceCountPoint> monthlyNewAccounts = [];
        if (organizationId == null)
        {
            var accountRows = await scopedAccounts
                .Where(account => account.OpenedAtUtc >= yearStart && account.OpenedAtUtc < yearEnd)
                .GroupBy(account => account.OpenedAtUtc.Month)
                .Select(group => new { Month = group.Key, Value = group.LongCount() })
                .ToListAsync(cancellationToken);
            monthlyNewAccounts = accountRows
                .Select(row => new AdminDashboardFinanceCountPoint(row.Month, row.Value))
                .ToArray();
        }

        IQueryable<TopUpRun> completedRuns =
            from run in dbContext.Set<TopUpRun>().AsNoTracking()
            join campaign in dbContext.Set<TopUpCampaign>().AsNoTracking()
                on run.TopUpCampaignId equals campaign.Id
            where (organizationId == null || campaign.OrganizationId == organizationId)
                && run.CompletedAtUtc != null
                && (run.RunStatusCode == TopUpRunStatusCodes.Completed
                    || run.RunStatusCode == TopUpRunStatusCodes.Partial)
            select run;

        DateTime monthStartUtc = monthStart.UtcDateTime;
        DateTime monthEndUtc = monthEnd.UtcDateTime;
        decimal topUpAmountThisMonth = await completedRuns
            .Where(run => run.CompletedAtUtc >= monthStartUtc && run.CompletedAtUtc < monthEndUtc)
            .SumAsync(run => (decimal?)run.TotalAmount, cancellationToken) ?? 0m;

        IReadOnlyCollection<AdminDashboardFinanceAmountPoint> monthlyTopUpAmounts = [];
        if (organizationId != null)
        {
            DateTime yearStartUtc = yearStart.UtcDateTime;
            DateTime yearEndUtc = yearEnd.UtcDateTime;
            var topUpRows = await completedRuns
                .Where(run => run.CompletedAtUtc >= yearStartUtc && run.CompletedAtUtc < yearEndUtc)
                .GroupBy(run => run.CompletedAtUtc!.Value.Month)
                .Select(group => new { Month = group.Key, Amount = group.Sum(run => run.TotalAmount) })
                .ToListAsync(cancellationToken);
            monthlyTopUpAmounts = topUpRows
                .Select(row => new AdminDashboardFinanceAmountPoint(row.Month, row.Amount))
                .ToArray();
        }

        long activeCampaigns = organizationId is long schoolId
            ? await dbContext.Set<TopUpCampaign>()
                .AsNoTracking()
                .LongCountAsync(campaign => campaign.OrganizationId == schoolId
                    && campaign.CampaignStatusCode == TopUpCampaignStatusCodes.Active, cancellationToken)
            : 0;

        return new AdminDashboardFinanceMetrics(
            totalActiveAccounts,
            newAccountsThisMonth,
            topUpAmountThisMonth,
            activeCampaigns,
            CurrencyCodes.SingaporeDollar,
            monthlyNewAccounts,
            monthlyTopUpAmounts);
    }
}
