using Microsoft.EntityFrameworkCore;
using Moe.Modules.EducationAccountTopUp.Domain.EducationAccounts;
using Moe.Modules.EducationAccountTopUp.IGateway.History;
using Moe.StudentFinance.Persistence;

namespace Moe.Modules.EducationAccountTopUp.Infrastructure.History;

internal sealed class AccountTransactionHistoryReader(MoeDbContext dbContext) : IAccountTransactionHistoryReader
{
    public async Task<HistoryPage<AccountTransactionHistoryProjection>> GetTransactionsAsync(
        long educationAccountId,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        int skip = Math.Max(page - 1, 0) * pageSize;

        IQueryable<AccountTransaction> query = dbContext.Set<AccountTransaction>()
            .AsNoTracking()
            .Where(x => x.EducationAccountId == educationAccountId);

        long totalCount = await query.LongCountAsync(cancellationToken);

        AccountTransactionHistoryProjection[] items = await query
            .OrderByDescending(x => x.TransactionAtUtc)
            .ThenByDescending(x => x.Id)
            .Skip(skip)
            .Take(pageSize)
            .Select(x => new AccountTransactionHistoryProjection(
                x.Id,
                x.TransactionAtUtc,
                x.TransactionTypeCode,
                x.Amount,
                x.ReferenceTypeCode,
                x.Description,
                x.BalanceAfter,
                x.CreatedByLoginAccountId))
            .ToArrayAsync(cancellationToken);

        return new HistoryPage<AccountTransactionHistoryProjection>(items, totalCount);
    }
}
