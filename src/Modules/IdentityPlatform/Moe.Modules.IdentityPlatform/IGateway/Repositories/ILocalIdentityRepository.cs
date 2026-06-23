using Moe.Modules.IdentityPlatform.Domain.Iam;
using Moe.Modules.IdentityPlatform.Domain.People;

namespace Moe.Modules.IdentityPlatform.IGateway.Repositories;

internal interface ILocalIdentityRepository
{
    Task<UserAccount?> FindUserAccountAsync(long userAccountId, CancellationToken cancellationToken);

    Task<Person?> FindPersonAsync(long personId, CancellationToken cancellationToken);
}
