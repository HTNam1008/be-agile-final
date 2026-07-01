using Moe.Modules.EducationAccountTopUp.Domain.Lifecycle;

namespace Moe.Modules.EducationAccountTopUp.IGateway.Repositories;

public interface IEducationAccountLifecycleRunRepository
{
    Task<bool> HasScheduledRunAsync(
        DateOnly runDateUtc,
        CancellationToken cancellationToken);

    Task AddAsync(
        EducationAccountLifecycleRun run,
        CancellationToken cancellationToken);
}
