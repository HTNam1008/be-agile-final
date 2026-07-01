using Moe.SharedKernel.Results;

namespace Moe.Modules.IdentityPlatform.IGateway.People;

public interface IPersonLifecycleGateway
{
    Task<Result> DisableAsync(long personId, DateTime utcNow, CancellationToken cancellationToken);

    Task<Result> EnableAsync(long personId, DateTime utcNow, CancellationToken cancellationToken);
}
