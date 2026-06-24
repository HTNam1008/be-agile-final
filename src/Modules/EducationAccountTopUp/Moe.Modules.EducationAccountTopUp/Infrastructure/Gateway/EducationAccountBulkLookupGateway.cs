using Microsoft.EntityFrameworkCore;
using Moe.Modules.EducationAccountTopUp.Domain.EducationAccounts;
using Moe.Modules.IdentityPlatform.IGateway.Accounts;
using Moe.StudentFinance.Persistence;

namespace Moe.Modules.EducationAccountTopUp.Infrastructure.Gateway;

internal sealed class EducationAccountBulkLookupGateway(MoeDbContext dbContext) : IEducationAccountBulkLookupGateway
{
    public async Task<IReadOnlyDictionary<long, EducationAccountLookupSummary>> FindByPersonIdsAsync(
        IReadOnlyCollection<long> personIds,
        CancellationToken cancellationToken)
    {
        if (personIds.Count == 0)
        {
            return new Dictionary<long, EducationAccountLookupSummary>();
        }

        EducationAccountLookupSummary[] accounts = await dbContext.Set<EducationAccount>()
            .AsNoTracking()
            .Where(x => personIds.Contains(x.PersonId))
            .Select(x => new EducationAccountLookupSummary(
                x.Id,
                x.PersonId,
                x.AccountNumber,
                CurrencyCodes.SingaporeDollar,
                x.StatusCode,
                x.CachedBalance))
            .ToArrayAsync(cancellationToken);

        return accounts.ToDictionary(x => x.PersonId);
    }
}
