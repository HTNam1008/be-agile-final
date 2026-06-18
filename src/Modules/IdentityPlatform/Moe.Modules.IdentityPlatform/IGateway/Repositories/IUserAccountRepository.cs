using Moe.Modules.IdentityPlatform.Domain.Iam;

namespace Moe.Modules.IdentityPlatform.IGateway.Repositories;

internal interface IUserAccountRepository
{
    Task<UserAccount?> FindByIdAsync(long userAccountId, CancellationToken cancellationToken);

    Task<bool> ExistsAdminByEmailAsync(string normalizedEmail, CancellationToken cancellationToken);

    Task<bool> ExistsSingpassForPersonAsync(long personId, CancellationToken cancellationToken);

    Task<UserAccount?> DisableAsync(long userAccountId, DateTime utcNow, CancellationToken cancellationToken);

    Task<UserAccount?> UpdateContactDetailsAsync(
        long userAccountId,
        string? contactEmail,
        string? contactMobile,
        DateTime utcNow,
        CancellationToken cancellationToken);
}
