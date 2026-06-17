using Moe.Modules.EducationAccountTopUp.Domain.TopUps;

namespace Moe.Modules.EducationAccountTopUp.IGateway.Repositories;

public interface ITopUpRunRepository
{
    Task<TopUpRun?> GetByIdAsync(long id, CancellationToken cancellationToken = default);
    Task<TopUpRun?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default);
    void Add(TopUpRun run);
}
