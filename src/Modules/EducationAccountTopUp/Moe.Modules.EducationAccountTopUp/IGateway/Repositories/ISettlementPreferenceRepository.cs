using Moe.Modules.EducationAccountTopUp.Domain.EducationAccounts;

namespace Moe.Modules.EducationAccountTopUp.IGateway.Repositories;

internal interface ISettlementPreferenceRepository
{
    Task<SettlementPreference?> FindActiveByEducationAccountIdAsync(
        long educationAccountId,
        CancellationToken cancellationToken);

    Task AddAsync(SettlementPreference preference, CancellationToken cancellationToken);
}
