using Moe.Modules.EducationAccountTopUp.Domain.EducationAccounts;

namespace Moe.Modules.EducationAccountTopUp.IGateway.Repositories;

internal interface IEducationAccountRepository
{
    Task<EducationAccount?> FindByPersonIdAsync(long personId, CancellationToken cancellationToken);

    Task<bool> ExistsForPersonAsync(long personId, CancellationToken cancellationToken);

    Task AddAsync(EducationAccount account, CancellationToken cancellationToken);
}
