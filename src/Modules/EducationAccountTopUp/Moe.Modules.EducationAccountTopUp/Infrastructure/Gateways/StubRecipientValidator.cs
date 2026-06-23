using Microsoft.EntityFrameworkCore;
using Moe.Modules.EducationAccountTopUp.Domain.EducationAccounts;
using Moe.Modules.EducationAccountTopUp.Domain.TopUps;
using Moe.Modules.EducationAccountTopUp.IGateway;
using Moe.SharedKernel.Results;
using Moe.StudentFinance.Persistence;

namespace Moe.Modules.EducationAccountTopUp.Infrastructure.Gateways;

internal sealed class StubRecipientValidator(MoeDbContext dbContext) : IRecipientValidator
{
    public async Task<Result> ValidateRecipientAsync(
        long educationAccountId,
        long organizationUnitId,
        CancellationToken cancellationToken = default)
    {
        _ = organizationUnitId;

        var account = await dbContext.Set<EducationAccount>()
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == educationAccountId, cancellationToken);

        if (account is null || account.StatusCode != AccountStatuses.Active)
        {
            return Result.Failure(TopUpErrors.RecipientNotEligible);
        }

        return Result.Success();
    }
}
