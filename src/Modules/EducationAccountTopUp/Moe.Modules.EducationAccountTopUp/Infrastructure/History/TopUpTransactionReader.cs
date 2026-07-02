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
        var runQuery = dbContext.Set<TopUpRun>().AsNoTracking()
            .Where(r => r.TopUpCampaignId == campaignId);

        if (filter.DateFromUtc.HasValue)
            runQuery = runQuery.Where(r => r.ScheduledForUtc >= filter.DateFromUtc.Value);
        if (filter.DateToUtc.HasValue)
            runQuery = runQuery.Where(r => r.ScheduledForUtc < filter.DateToUtc.Value);

        var campaignQuery = dbContext.Set<TopUpCampaign>().AsNoTracking();
        var txnQuery = dbContext.Set<TopUpTransaction>().AsNoTracking();

        if (!string.IsNullOrWhiteSpace(filter.Status))
            txnQuery = txnQuery.Where(x => x.TransactionStatusCode == filter.Status.Trim().ToUpperInvariant());

        var query =
            from run in runQuery
            join txn in txnQuery on run.Id equals txn.TopUpRunId
            join campaign in campaignQuery on run.TopUpCampaignId equals campaign.Id
            select new
            {
                TransactionId = txn.Id,
                RunId = txn.TopUpRunId,
                txn.EducationAccountId,
                txn.Amount,
                StatusCode = txn.TransactionStatusCode,
                txn.Reason,
                txn.CreatedAtUtc,
                txn.CompletedAtUtc,
                RunDateUtc = run.ScheduledForUtc,
                campaign.CampaignCode,
                campaign.CampaignName
            };

        long totalCount = await query.LongCountAsync(cancellationToken);
        TransactionHistoryProjection[] items = await query
            .OrderByDescending(x => x.CreatedAtUtc)
            .ThenByDescending(x => x.TransactionId)
            .Skip(checked((page - 1) * pageSize))
            .Take(pageSize)
            .Select(x => new TransactionHistoryProjection(
                x.TransactionId,
                x.RunId,
                x.EducationAccountId,
                x.Amount,
                x.StatusCode,
                x.Reason,
                x.CreatedAtUtc,
                x.CompletedAtUtc,
                x.RunDateUtc,
                x.CampaignCode,
                x.CampaignName))
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
            select new
            {
                TransactionId = txn.Id,
                RunId = txn.TopUpRunId,
                txn.EducationAccountId,
                txn.Amount,
                StatusCode = txn.TransactionStatusCode,
                txn.Reason,
                txn.CreatedAtUtc,
                txn.CompletedAtUtc,
                RunDateUtc = run.ScheduledForUtc,
                campaign.CampaignCode,
                campaign.CampaignName
            };

        long totalCount = await query.LongCountAsync(cancellationToken);
        TransactionHistoryProjection[] items = await query
            .OrderByDescending(x => x.CreatedAtUtc)
            .ThenByDescending(x => x.TransactionId)
            .Skip(checked((page - 1) * pageSize))
            .Take(pageSize)
            .Select(x => new TransactionHistoryProjection(
                x.TransactionId,
                x.RunId,
                x.EducationAccountId,
                x.Amount,
                x.StatusCode,
                x.Reason,
                x.CreatedAtUtc,
                x.CompletedAtUtc,
                x.RunDateUtc,
                x.CampaignCode,
                x.CampaignName))
            .ToArrayAsync(cancellationToken);

        return new TransactionHistoryPage(items, totalCount);
    }

    public async Task<TransactionHistoryPage> GetAllTransactionsAsync(
        TopUpHistoryFilter filter,
        int page,
        int pageSize,
        string? sortBy,
        string? sortDirection,
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
            select new
            {
                TransactionId = txn.Id,
                RunId = txn.TopUpRunId,
                txn.EducationAccountId,
                txn.Amount,
                StatusCode = txn.TransactionStatusCode,
                txn.Reason,
                txn.CreatedAtUtc,
                txn.CompletedAtUtc,
                RunDateUtc = run.ScheduledForUtc,
                campaign.CampaignCode,
                campaign.CampaignName
            };

        if (!string.IsNullOrWhiteSpace(filter.CampaignSearch))
        {
            string search = $"%{filter.CampaignSearch.Trim()}%";
            query = query.Where(x =>
                EF.Functions.Like(x.CampaignCode, search)
                || EF.Functions.Like(x.CampaignName, search));
        }

        bool descending = string.Equals(sortDirection, "desc", StringComparison.OrdinalIgnoreCase);
        string sortKey = sortBy?.Trim().ToLowerInvariant() ?? string.Empty;
        var orderedQuery = sortKey switch
        {
            "account" => descending
                ? query.OrderByDescending(x => x.EducationAccountId).ThenByDescending(x => x.TransactionId)
                : query.OrderBy(x => x.EducationAccountId).ThenByDescending(x => x.TransactionId),
            "campaign" => descending
                ? query.OrderByDescending(x => x.CampaignCode).ThenByDescending(x => x.TransactionId)
                : query.OrderBy(x => x.CampaignCode).ThenByDescending(x => x.TransactionId),
            "amount" => descending
                ? query.OrderByDescending(x => x.Amount).ThenByDescending(x => x.TransactionId)
                : query.OrderBy(x => x.Amount).ThenByDescending(x => x.TransactionId),
            "rundate" => descending
                ? query.OrderByDescending(x => x.RunDateUtc).ThenByDescending(x => x.TransactionId)
                : query.OrderBy(x => x.RunDateUtc).ThenByDescending(x => x.TransactionId),
            "status" => descending
                ? query.OrderByDescending(x => x.StatusCode).ThenByDescending(x => x.TransactionId)
                : query.OrderBy(x => x.StatusCode).ThenByDescending(x => x.TransactionId),
            "completed" => descending
                ? query.OrderByDescending(x => x.CompletedAtUtc).ThenByDescending(x => x.TransactionId)
                : query.OrderBy(x => x.CompletedAtUtc).ThenByDescending(x => x.TransactionId),
            _ => query.OrderByDescending(x => x.CreatedAtUtc).ThenByDescending(x => x.TransactionId)
        };

        long totalCount = await query.LongCountAsync(cancellationToken);
        TransactionHistoryProjection[] items = await orderedQuery
            .Skip(checked((page - 1) * pageSize))
            .Take(pageSize)
            .Select(x => new TransactionHistoryProjection(
                x.TransactionId,
                x.RunId,
                x.EducationAccountId,
                x.Amount,
                x.StatusCode,
                x.Reason,
                x.CreatedAtUtc,
                x.CompletedAtUtc,
                x.RunDateUtc,
                x.CampaignCode,
                x.CampaignName))
            .ToArrayAsync(cancellationToken);

        return new TransactionHistoryPage(items, totalCount);
    }
}
