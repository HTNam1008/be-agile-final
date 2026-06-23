using Moe.Modules.IdentityPlatform.Domain.Iam;
using Moe.Modules.IdentityPlatform.Domain.People;

namespace Moe.Modules.IdentityPlatform.IGateway.Repositories;

internal interface IStudentSingpassProvisioningRepository
{
    Task<Person?> FindPersonAsync(long personId, CancellationToken cancellationToken);

    Task<UserAccount?> FindSingpassAccountForRequestAsync(
        IdentityProvisioningRequest request,
        CancellationToken cancellationToken);

    Task AddAccountAndRequestAsync(
        UserAccount account,
        IdentityProvisioningRequest request,
        CancellationToken cancellationToken);

    Task EnsureActiveStudentScopeAsync(
        long userAccountId,
        long actorUserAccountId,
        DateTime utcNow,
        CancellationToken cancellationToken);
}
