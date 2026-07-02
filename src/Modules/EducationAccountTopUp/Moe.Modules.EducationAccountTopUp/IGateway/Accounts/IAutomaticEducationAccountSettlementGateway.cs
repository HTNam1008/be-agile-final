using Moe.Modules.EducationAccountTopUp.Domain.EducationAccounts;

namespace Moe.Modules.EducationAccountTopUp.IGateway.Accounts;

internal interface IAutomaticEducationAccountSettlementGateway
{
    Task SettleRemainingBalanceAsync(
        EducationAccount account,
        DateTimeOffset settledAtUtc,
        CancellationToken cancellationToken);
}
