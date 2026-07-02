using Moe.Application.Abstractions.Security;
using Moe.Modules.IdentityPlatform.Domain.Iam;
using Moe.Modules.IdentityPlatform.Domain.People;
using Moe.Modules.IdentityPlatform.IGateway.Accounts;
using Moe.Modules.IdentityPlatform.IGateway.Authentication;
using Moe.Modules.IdentityPlatform.IGateway.Repositories;

namespace Moe.Modules.IdentityPlatform.Infrastructure.Authentication;

internal sealed class LocalIdentityDirectory(
    ICurrentUser currentUser,
    ILocalIdentityRepository localIdentity,
    IEducationAccountProvisioningGateway educationAccountProvisioning) : ILocalIdentityDirectory
{
    public async Task<LocalIdentitySummary?> GetCurrentAsync(CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserAccountId is null)
        {
            return null;
        }

        UserAccount? account = await localIdentity.FindUserAccountAsync(currentUser.UserAccountId.Value, cancellationToken);

        if (account is null)
        {
            return null;
        }

        Person? person = account.PersonId is long personId
            ? await localIdentity.FindPersonAsync(personId, cancellationToken)
            : null;
        int? age = person is null ? null : CalculateAge(person.DateOfBirth, DateOnly.FromDateTime(DateTime.UtcNow));
        bool isAccountHolder = account.PersonId is long accountPersonId
            && await educationAccountProvisioning.HasAccountAsync(accountPersonId, cancellationToken);

        LocalIdentitySummary summary = new(
            account.Id,
            account.PersonId,
            account.DisplayNameSnapshot ?? string.Empty,
            account.IdentityProviderCode,
            currentUser.Portal,
            account.RoleCode,
            account.AccountStatusCode,
            age,
            isAccountHolder,
            currentUser.OrganizationUnitIds,
            currentUser.Roles,
            currentUser.Permissions);

        return summary;
    }

    private static int CalculateAge(DateOnly dateOfBirth, DateOnly today)
    {
        int age = today.Year - dateOfBirth.Year;

        if (dateOfBirth > today.AddYears(-age))
        {
            age--;
        }

        return age;
    }
}
