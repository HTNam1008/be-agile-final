using Microsoft.EntityFrameworkCore;
using Moe.Modules.EducationAccountTopUp.Application.EducationAccounts.GetMyEducationAccount;
using Moe.Modules.EducationAccountTopUp.Domain.EducationAccounts;
using Moe.Modules.EducationAccountTopUp.IGateway.EducationAccounts;
using Moe.StudentFinance.Persistence;

namespace Moe.Modules.EducationAccountTopUp.Infrastructure.EducationAccounts;

internal sealed class EducationAccountReader(MoeDbContext dbContext) : IEducationAccountReader
{
    public async Task<MyEducationAccountDto?> GetMyEducationAccountAsync(long personId, CancellationToken cancellationToken = default)
    {
        return await dbContext.Set<EducationAccount>()
            .AsNoTracking()
            .Where(x => x.PersonId == personId)
            .Select(x => new MyEducationAccountDto(
                x.Id,
                x.PersonId,
                x.AccountNumber,
                CurrencyCodes.SingaporeDollar,
                x.StatusCode,
                x.CachedBalance,
                x.OpenedAtUtc,
                x.OpeningModeCode,
                x.OpeningRemarks,
                x.PendingClosureAtUtc,
                x.ClosedAtUtc,
                new
                {
                    items = Array.Empty<object>(),
                    page = 1,
                    pageSize = 10,
                    totalCount = 0
                }
            ))
            .SingleOrDefaultAsync(cancellationToken);
    }
}
