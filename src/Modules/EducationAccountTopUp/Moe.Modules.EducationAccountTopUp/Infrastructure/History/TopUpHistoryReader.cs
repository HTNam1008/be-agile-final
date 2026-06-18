using Microsoft.EntityFrameworkCore;
using Moe.Modules.EducationAccountTopUp.Application.History;
using Moe.Modules.EducationAccountTopUp.Domain.EducationAccounts;
using Moe.Modules.EducationAccountTopUp.Domain.TopUps;
using Moe.Modules.EducationAccountTopUp.IGateway.History;
using Moe.Modules.IdentityPlatform.Domain.People;
using Moe.StudentFinance.Persistence;

namespace Moe.Modules.EducationAccountTopUp.Infrastructure.History;

internal sealed class TopUpHistoryReader(MoeDbContext dbContext) : ITopUpHistoryReader
{
    public async Task<HistoryPage<CampaignHistoryProjection>> GetCampaignHistoryAsync(
        TopUpHistoryFilter filter,
        TopUpAccessScope accessScope,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        IQueryable<TopUpCampaign> query = dbContext.Set<TopUpCampaign>().AsNoTracking();

        if (accessScope.RequiresOrganizationFilter)
        {
            query = query.Where(x => accessScope.OrganizationIds.Contains(x.OrganizationId));
        }

        if (filter.DateFromUtc.HasValue)
        {
            query = query.Where(x => x.CreatedAtUtc >= filter.DateFromUtc.Value);
        }

        if (filter.DateToUtc.HasValue)
        {
            query = query.Where(x => x.CreatedAtUtc < filter.DateToUtc.Value);
        }

        if (filter.CampaignId.HasValue)
        {
            query = query.Where(x => x.Id == filter.CampaignId.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.CampaignSearch))
        {
            string search = filter.CampaignSearch.Trim();
            query = query.Where(x =>
                x.CampaignCode.Contains(search)
                || x.CampaignName.Contains(search));
        }

        if (!string.IsNullOrWhiteSpace(filter.Status))
        {
            string[] statuses = BuildCodeVariants(filter.Status);
            query = query.Where(x => statuses.Contains(x.CampaignStatusCode));
        }

        if (filter.ActorId.HasValue)
        {
            query = query.Where(x =>
                x.CreatedByLoginAccountId == filter.ActorId.Value
                || x.UpdatedByLoginAccountId == filter.ActorId.Value);
        }

        long totalCount = await query.LongCountAsync(cancellationToken);
        CampaignHistoryProjection[] items = await query
            .OrderByDescending(x => x.CreatedAtUtc)
            .ThenByDescending(x => x.Id)
            .Skip(CalculateSkip(page, pageSize))
            .Take(pageSize)
            .Select(x => new CampaignHistoryProjection(
                x.Id,
                x.CampaignCode,
                x.CampaignName,
                x.OrganizationId,
                x.CampaignVersion,
                x.ScheduleTypeCode,
                x.StartDate,
                x.EndDate,
                x.NextRunAtUtc,
                x.CampaignStatusCode,
                x.CreatedByLoginAccountId,
                x.CreatedAtUtc,
                x.UpdatedByLoginAccountId,
                x.UpdatedAtUtc))
            .ToArrayAsync(cancellationToken);

        return new HistoryPage<CampaignHistoryProjection>(items, totalCount);
    }

    public async Task<HistoryPage<RunHistoryProjection>> GetRunHistoryAsync(
        TopUpHistoryFilter filter,
        TopUpAccessScope accessScope,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        IQueryable<TopUpRun> runQuery = dbContext.Set<TopUpRun>().AsNoTracking();
        IQueryable<TopUpCampaign> campaignQuery = dbContext.Set<TopUpCampaign>().AsNoTracking();

        if (accessScope.RequiresOrganizationFilter)
        {
            campaignQuery = campaignQuery.Where(
                x => accessScope.OrganizationIds.Contains(x.OrganizationId));
        }

        if (filter.DateFromUtc.HasValue)
        {
            runQuery = runQuery.Where(x => x.ScheduledForUtc >= filter.DateFromUtc.Value);
        }

        if (filter.DateToUtc.HasValue)
        {
            runQuery = runQuery.Where(x => x.ScheduledForUtc < filter.DateToUtc.Value);
        }

        if (filter.CampaignId.HasValue)
        {
            runQuery = runQuery.Where(x => x.TopUpCampaignId == filter.CampaignId.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.CampaignSearch))
        {
            string search = filter.CampaignSearch.Trim();
            campaignQuery = campaignQuery.Where(x =>
                x.CampaignCode.Contains(search)
                || x.CampaignName.Contains(search));
        }

        if (!string.IsNullOrWhiteSpace(filter.TriggerType))
        {
            string[] triggerTypes = BuildCodeVariants(filter.TriggerType);
            runQuery = runQuery.Where(x => triggerTypes.Contains(x.TriggerTypeCode));
        }

        if (!string.IsNullOrWhiteSpace(filter.Status))
        {
            string[] statuses = BuildCodeVariants(filter.Status);
            runQuery = runQuery.Where(x => statuses.Contains(x.RunStatusCode));
        }

        if (filter.ActorId.HasValue)
        {
            runQuery = runQuery.Where(
                x => x.TriggeredByLoginAccountId == filter.ActorId.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.StudentOrAccountSearch))
        {
            string search = filter.StudentOrAccountSearch.Trim();
            IQueryable<long> matchingAccountIds = dbContext.Set<EducationAccount>()
                .AsNoTracking()
                .Where(account =>
                    account.AccountNumber.Contains(search)
                    || dbContext.Set<Person>().Any(person =>
                        person.Id == account.PersonId
                        && (person.OfficialFullName.Contains(search)
                            || person.ExternalPersonReference.Contains(search))))
                .Select(account => account.Id);

            runQuery = runQuery.Where(x => dbContext.Set<TopUpTransaction>().Any(transaction =>
                transaction.TopUpRunId == x.Id
                && matchingAccountIds.Contains(transaction.EducationAccountId)));
        }

        var query =
            from run in runQuery
            join campaign in campaignQuery
                on run.TopUpCampaignId equals campaign.Id
            select new { Run = run, Campaign = campaign };

        long totalCount = await query.LongCountAsync(cancellationToken);
        RunHistoryProjection[] items = await query
            .OrderByDescending(x => x.Run.ScheduledForUtc)
            .ThenByDescending(x => x.Run.Id)
            .Skip(CalculateSkip(page, pageSize))
            .Take(pageSize)
            .Select(x => new RunHistoryProjection(
                x.Run.Id,
                x.Run.TopUpCampaignId,
                x.Campaign.CampaignCode,
                x.Campaign.CampaignName,
                x.Campaign.OrganizationId,
                x.Run.ScheduledForUtc,
                x.Run.TriggerTypeCode,
                x.Run.RunStatusCode,
                x.Run.TotalSelected,
                x.Run.TotalProcessed,
                x.Run.TotalSucceeded,
                x.Run.TotalFailed,
                x.Run.TotalAmount,
                x.Run.TriggeredByLoginAccountId,
                x.Run.StartedAtUtc,
                x.Run.CompletedAtUtc))
            .ToArrayAsync(cancellationToken);

        return new HistoryPage<RunHistoryProjection>(items, totalCount);
    }

    private static int CalculateSkip(int page, int pageSize)
        => checked((page - 1) * pageSize);

    private static string[] BuildCodeVariants(string value)
    {
        string trimmed = value.Trim();
        string upper = trimmed.ToUpperInvariant();
        string pascal = upper.Length == 0
            ? upper
            : string.Concat(upper[0], upper[1..].ToLowerInvariant());

        return [trimmed, upper, pascal];
    }

}
