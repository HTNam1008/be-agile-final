using Microsoft.EntityFrameworkCore;
using Moe.Modules.EducationAccountTopUp.Domain.EducationAccounts;
using Moe.Modules.EducationAccountTopUp.IGateway.TopUps;
using Moe.StudentFinance.Persistence;

namespace Moe.Modules.EducationAccountTopUp.Infrastructure.Repositories;

internal sealed class TopUpAccountProjectionRepository(MoeDbContext dbContext) : ITopUpAccountProjectionRepository
{
    public async Task<IReadOnlyCollection<long>> FindMatchingPersonIdsAsync(
        TopUpAccountSearchCriteria criteria,
        CancellationToken cancellationToken)
    {
        IQueryable<EducationAccount> query = dbContext.Set<EducationAccount>().AsNoTracking();

        if (!string.IsNullOrWhiteSpace(criteria.Search))
        {
            string search = criteria.Search.Trim();
            query = query.Where(x => x.AccountNumber.Contains(search));
        }

        if (criteria.BalanceFrom.HasValue)
        {
            query = query.Where(x => x.CachedBalance >= criteria.BalanceFrom.Value);
        }

        if (criteria.BalanceTo.HasValue)
        {
            query = query.Where(x => x.CachedBalance <= criteria.BalanceTo.Value);
        }

        if (!string.IsNullOrWhiteSpace(criteria.AccountStatusCode))
        {
            string accountStatusCode = criteria.AccountStatusCode.Trim();
            query = query.Where(x => x.StatusCode == accountStatusCode);
        }

        return await query
            .Select(x => x.PersonId)
            .Distinct()
            .ToArrayAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<long, TopUpAccountProjection>> FindByPersonIdsAsync(
        IReadOnlyCollection<long> personIds,
        CancellationToken cancellationToken)
    {
        if (personIds.Count == 0)
        {
            return new Dictionary<long, TopUpAccountProjection>();
        }

        TopUpAccountProjection[] accounts = await dbContext.Set<EducationAccount>()
            .AsNoTracking()
            .Where(x => personIds.Contains(x.PersonId))
            .Select(x => new TopUpAccountProjection(
                x.PersonId,
                x.Id,
                x.AccountNumber,
                x.StatusCode,
                x.CachedBalance))
            .ToArrayAsync(cancellationToken);

        return accounts.ToDictionary(x => x.PersonId);
    }

    public async Task<IReadOnlyDictionary<long, TopUpAccountProjection>> FindByEducationAccountIdsAsync(
        IReadOnlyCollection<long> educationAccountIds,
        CancellationToken cancellationToken)
    {
        if (educationAccountIds.Count == 0)
        {
            return new Dictionary<long, TopUpAccountProjection>();
        }

        TopUpAccountProjection[] accounts = await dbContext.Set<EducationAccount>()
            .AsNoTracking()
            .Where(x => educationAccountIds.Contains(x.Id))
            .Select(x => new TopUpAccountProjection(
                x.PersonId,
                x.Id,
                x.AccountNumber,
                x.StatusCode,
                x.CachedBalance))
            .ToArrayAsync(cancellationToken);

        return accounts.ToDictionary(x => x.EducationAccountId);
    }
}
