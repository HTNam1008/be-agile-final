using Microsoft.EntityFrameworkCore;
using Moe.Modules.EducationAccountTopUp.Application.History;
using Moe.Modules.EducationAccountTopUp.Domain.TopUps;
using Moe.Modules.EducationAccountTopUp.IGateway.History;
using Moe.StudentFinance.Persistence;

namespace Moe.Modules.EducationAccountTopUp.Infrastructure.History;

internal sealed class TopUpTransactionReader(MoeDbContext dbContext) : ITopUpTransactionReader
{
    public async Task<TransactionHistoryPage> GetCampaignTransactionsAsync(
        long campaignId,
        TopUpHistoryFilter filter,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = BuildCampaignQuery(campaignId, filter);

        long totalCount = await query.LongCountAsync(cancellationToken);
        TransactionHistoryProjection[] items = await query
            .OrderByDescending(x => x.CreatedAtUtc)
            .ThenByDescending(x => x.TransactionId)
            .Skip(checked((page - 1) * pageSize))
            .Take(pageSize)
            .ToArrayAsync(cancellationToken);

        return new TransactionHistoryPage(items, totalCount);
    }

    public async Task<TransactionHistoryPage> GetAccountTransactionsAsync(
        long educationAccountId,
        TopUpHistoryFilter filter,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var runQuery = dbContext.Set<TopUpRun>().AsNoTracking();
        var campaignQuery = dbContext.Set<TopUpCampaign>().AsNoTracking();
        var txnQuery = dbContext.Set<TopUpTransaction>().AsNoTracking()
            .Where(x => x.EducationAccountId == educationAccountId);

        if (filter.DateFromUtc.HasValue)
            txnQuery = txnQuery.Where(x => x.CreatedAtUtc >= filter.DateFromUtc.Value);
        if (filter.DateToUtc.HasValue)
            txnQuery = txnQuery.Where(x => x.CreatedAtUtc < filter.DateToUtc.Value);
        if (!string.IsNullOrWhiteSpace(filter.Status))
            txnQuery = txnQuery.Where(x => x.TransactionStatusCode == filter.Status.Trim().ToUpperInvariant());

        var query =
            from txn in txnQuery
            join run in runQuery on txn.TopUpRunId equals run.Id
            join campaign in campaignQuery on run.TopUpCampaignId equals campaign.Id
            select new TransactionHistoryProjection(
                txn.Id,
                txn.TopUpRunId,
                txn.EducationAccountId,
                txn.Amount,
                txn.TransactionStatusCode,
                txn.Reason,
                txn.CreatedAtUtc,
                txn.CompletedAtUtc,
                run.ScheduledForUtc,
                campaign.CampaignCode,
                campaign.CampaignName);

        long totalCount = await query.LongCountAsync(cancellationToken);
        TransactionHistoryProjection[] items = await query
            .OrderByDescending(x => x.CreatedAtUtc)
            .ThenByDescending(x => x.TransactionId)
            .Skip(checked((page - 1) * pageSize))
            .Take(pageSize)
            .ToArrayAsync(cancellationToken);

        return new TransactionHistoryPage(items, totalCount);
    }

    private IQueryable<TransactionHistoryProjection> BuildCampaignQuery(long campaignId, TopUpHistoryFilter filter)
    {
        var runQuery = dbContext.Set<TopUpRun>().AsNoTracking()
            .Where(r => r.TopUpCampaignId == campaignId);

        if (filter.DateFromUtc.HasValue)
            runQuery = runQuery.Where(r => r.ScheduledForUtc >= filter.DateFromUtc.Value);
        if (filter.DateToUtc.HasValue)
            runQuery = runQuery.Where(r => r.ScheduledForUtc < filter.DateToUtc.Value);

        var campaignQuery = dbContext.Set<TopUpCampaign>().AsNoTracking();
        var txnQuery = dbContext.Set<TopUpTransaction>().AsNoTracking();

        var query =
            from run in runQuery
            join txn in txnQuery on run.Id equals txn.TopUpRunId
            join campaign in campaignQuery on run.TopUpCampaignId equals campaign.Id
            select new TransactionHistoryProjection(
                txn.Id,
                txn.TopUpRunId,
                txn.EducationAccountId,
                txn.Amount,
                txn.TransactionStatusCode,
                txn.Reason,
                txn.CreatedAtUtc,
                txn.CompletedAtUtc,
                run.ScheduledForUtc,
                campaign.CampaignCode,
                campaign.CampaignName);

        if (!string.IsNullOrWhiteSpace(filter.Status))
        {
            string status = filter.Status.Trim().ToUpperInvariant();
            query = query.Where(x => x.StatusCode == status);
        }

        return query;
    }

    public async Task<TransactionHistoryPage> GetAllTransactionsAsync(
        TopUpHistoryFilter filter,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var runQuery = dbContext.Set<TopUpRun>().AsNoTracking();
        var campaignQuery = dbContext.Set<TopUpCampaign>().AsNoTracking();
        var txnQuery = dbContext.Set<TopUpTransaction>().AsNoTracking();

        if (filter.DateFromUtc.HasValue)
            txnQuery = txnQuery.Where(x => x.CreatedAtUtc >= filter.DateFromUtc.Value);
        if (filter.DateToUtc.HasValue)
            txnQuery = txnQuery.Where(x => x.CreatedAtUtc < filter.DateToUtc.Value);
        if (!string.IsNullOrWhiteSpace(filter.Status))
            txnQuery = txnQuery.Where(x => x.TransactionStatusCode == filter.Status.Trim().ToUpperInvariant());

        var query =
            from txn in txnQuery
            join run in runQuery on txn.TopUpRunId equals run.Id
            join campaign in campaignQuery on run.TopUpCampaignId equals campaign.Id
            select new TransactionHistoryProjection(
                txn.Id,
                txn.TopUpRunId,
                txn.EducationAccountId,
                txn.Amount,
                txn.TransactionStatusCode,
                txn.Reason,
                txn.CreatedAtUtc,
                txn.CompletedAtUtc,
                run.ScheduledForUtc,
                campaign.CampaignCode,
                campaign.CampaignName);

        if (!string.IsNullOrWhiteSpace(filter.CampaignSearch))
        {
            string search = filter.CampaignSearch.Trim().ToUpperInvariant();
            query = query.Where(x =>
                x.CampaignCode.ToUpperInvariant().Contains(search)
                || x.CampaignName.ToUpperInvariant().Contains(search));
        }

        long totalCount = await query.LongCountAsync(cancellationToken);
        TransactionHistoryProjection[] items = await query
            .OrderByDescending(x => x.CreatedAtUtc)
            .ThenByDescending(x => x.TransactionId)
            .Skip(checked((page - 1) * pageSize))
            .Take(pageSize)
            .ToArrayAsync(cancellationToken);

        return new TransactionHistoryPage(items, totalCount);
    }
}
