using Microsoft.EntityFrameworkCore;
using Moe.Modules.EducationAccountTopUp.Domain.EducationAccounts;
using Moe.Modules.EducationAccountTopUp.IGateway.Accounts;
using Moe.StudentFinance.Persistence;

namespace Moe.Modules.EducationAccountTopUp.Infrastructure.Gateway;

internal sealed class AutomaticEducationAccountSettlementGateway(MoeDbContext dbContext)
    : IAutomaticEducationAccountSettlementGateway
{
    private const string TransactionTypeCode = "AUTO_CLOSE_SETTLEMENT";
    private const string ReferenceTypeCode = "AUTO_CLOSE";
    private const string Description = "Auto-close settlement to CPF preference";

    public async Task SettleRemainingBalanceAsync(
        EducationAccount account,
        DateTimeOffset settledAtUtc,
        CancellationToken cancellationToken)
    {
        string idempotencyKey = $"AUTO-CLOSE-SETTLEMENT:{account.Id}";
        AccountTransaction? existing = await dbContext.Set<AccountTransaction>()
            .AsNoTracking()
            .SingleOrDefaultAsync(
                transaction => transaction.IdempotencyKey == idempotencyKey,
                cancellationToken);
        if (existing is not null)
        {
            return;
        }

        if (account.CachedBalance == 0m)
        {
            return;
        }

        decimal settlementAmount = -account.CachedBalance;
        AccountTransaction settlement = AccountTransaction.Create(
            account.Id,
            TransactionTypeCode,
            settlementAmount,
            ReferenceTypeCode,
            account.Id,
            idempotencyKey,
            account.CachedBalance,
            Description,
            createdByUserId: null,
            settledAtUtc.UtcDateTime);

        account.UpdateBalance(settlementAmount);
        await dbContext.Set<AccountTransaction>().AddAsync(settlement, cancellationToken);
    }
}
