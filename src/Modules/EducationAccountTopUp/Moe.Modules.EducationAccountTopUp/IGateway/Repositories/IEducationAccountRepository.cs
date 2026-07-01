using Moe.Modules.EducationAccountTopUp.Domain.EducationAccounts;

namespace Moe.Modules.EducationAccountTopUp.IGateway.Repositories;

public interface IEducationAccountRepository
{
    Task<EducationAccount?> FindByIdAsync(long educationAccountId, CancellationToken cancellationToken);

    Task<EducationAccount?> FindByPersonIdAsync(long personId, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<EducationAccount>> ListActiveAsync(CancellationToken cancellationToken);

    Task<bool> ExistsForPersonAsync(long personId, CancellationToken cancellationToken);

    Task AddAsync(EducationAccount account, CancellationToken cancellationToken);
}
