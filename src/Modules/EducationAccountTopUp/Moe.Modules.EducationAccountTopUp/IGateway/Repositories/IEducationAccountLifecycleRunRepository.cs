using Moe.Modules.EducationAccountTopUp.Domain.Lifecycle;

namespace Moe.Modules.EducationAccountTopUp.IGateway.Repositories;

public interface IEducationAccountLifecycleRunRepository
{
    Task AddAsync(
        EducationAccountLifecycleRun run,
        CancellationToken cancellationToken);
}
