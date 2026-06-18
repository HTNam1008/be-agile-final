using Microsoft.EntityFrameworkCore;
using Moe.Modules.EducationAccountTopUp.Application.RunExecution.TransactionResults;
using Moe.Modules.EducationAccountTopUp.Domain.TopUps;
using Moe.Modules.EducationAccountTopUp.IGateway.TransactionResults;
using Moe.StudentFinance.Persistence;

namespace Moe.Modules.EducationAccountTopUp.Infrastructure.TransactionResults;

internal sealed class TopUpTransactionResultsReader(MoeDbContext dbContext)
    : ITopUpTransactionResultsReader
{
    public async Task<TransactionResultsPage> GetPageAsync(
        long runId,
        TopUpTransactionResultFilter filter,
        IReadOnlyCollection<long>? matchingEducationAccountIds,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        IQueryable<TopUpTransaction> query = dbContext.Set<TopUpTransaction>()
            .AsNoTracking()
            .Where(x => x.TopUpRunId == runId);

        if (!string.IsNullOrWhiteSpace(filter.Status))
        {
            string status = filter.Status.Trim().ToUpperInvariant();
            query = query.Where(x => x.TransactionStatusCode == status);
        }

        if (!string.IsNullOrWhiteSpace(filter.Reason))
        {
            string reason = filter.Reason.Trim();
            query = query.Where(x => x.Reason != null && x.Reason.Contains(reason));
        }

        if (filter.DateFromUtc.HasValue)
        {
            query = query.Where(x => x.CreatedAtUtc >= filter.DateFromUtc.Value);
        }

        if (filter.DateToUtc.HasValue)
        {
            query = query.Where(x => x.CreatedAtUtc < filter.DateToUtc.Value);
        }

        if (matchingEducationAccountIds is not null)
        {
            query = matchingEducationAccountIds.Count == 0
                ? query.Where(_ => false)
                : query.Where(x => matchingEducationAccountIds.Contains(x.EducationAccountId));
        }

        long totalCount = await query.LongCountAsync(cancellationToken);
        TopUpTransactionResultProjection[] items = await query
            .OrderByDescending(x => x.CreatedAtUtc)
            .ThenByDescending(x => x.Id)
            .Skip(checked((page - 1) * pageSize))
            .Take(pageSize)
            .Select(x => new TopUpTransactionResultProjection(
                x.Id,
                x.EducationAccountId,
                x.Amount,
                x.TransactionStatusCode,
                x.Reason,
                x.AccountTransactionId,
                x.CreatedAtUtc,
                x.CompletedAtUtc))
            .ToArrayAsync(cancellationToken);

        return new TransactionResultsPage(items, totalCount);
    }
}
