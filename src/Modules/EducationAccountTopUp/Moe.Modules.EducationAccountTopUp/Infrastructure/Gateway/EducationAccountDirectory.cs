using Microsoft.EntityFrameworkCore;
using Moe.Modules.EducationAccountTopUp.Domain.EducationAccounts;
using Moe.Modules.EducationAccountTopUp.IGateway.Accounts;
using Moe.StudentFinance.Persistence;

namespace Moe.Modules.EducationAccountTopUp.Infrastructure.Gateway;

internal sealed class EducationAccountDirectory(MoeDbContext dbContext) : IEducationAccountDirectory
{
    public Task<EducationAccountSummary?> FindByPersonIdAsync(long personId, CancellationToken cancellationToken)
    {
        return dbContext.Set<EducationAccount>()
            .AsNoTracking()
            .Where(x => x.PersonId == personId)
            .Select(x => new EducationAccountSummary(
                x.Id,
                x.PersonId,
                x.AccountNumber,
                CurrencyCodes.SingaporeDollar,
                x.StatusCode,
                x.CachedBalance))
            .SingleOrDefaultAsync(cancellationToken);
    }
}
