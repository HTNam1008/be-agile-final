using Moe.Application.Abstractions.Clock;
using Moe.Application.Abstractions.Messaging;
using Moe.Application.Abstractions.Security;
using Moe.Modules.IdentityPlatform.Application;
using Moe.Modules.IdentityPlatform.Application.Organizations;
using Moe.Modules.IdentityPlatform.Domain.Iam;
using Moe.Modules.IdentityPlatform.IGateway.Repositories;
using Moe.SharedKernel.Results;

namespace Moe.Modules.IdentityPlatform.Application.AdminProfile.UpdateMyAdminContact;

internal sealed class UpdateMyAdminContactHandler(
    ICurrentUser currentUser,
    IUserAccountRepository userAccounts,
    IOrganizationUnitRepository organizations,
    IClock clock)
    : ICommandHandler<UpdateMyAdminContactCommand, AdminProfileResponse>
{
    public async Task<Result<AdminProfileResponse>> Handle(UpdateMyAdminContactCommand command, CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserAccountId is null || currentUser.Portal != PortalAccessCodes.Admin)
        {
            return Result<AdminProfileResponse>.Failure(IdentityErrors.AuthenticatedAdminRequired);
        }

        UserAccount? account = await userAccounts.UpdateContactDetailsAsync(
            currentUser.UserAccountId.Value,
            command.ContactEmail,
            command.ContactMobile,
            clock.UtcNow.UtcDateTime,
            cancellationToken);

        if (account is null || account.PortalAccessCode != PortalAccessCodes.Admin)
        {
            return Result<AdminProfileResponse>.Failure(IdentityErrors.UserAccountNotFound);
        }

        OrganizationUnitSummary? organization = account.AdminOrganizationId is long organizationId
            ? await organizations.FindActiveByIdAsync(organizationId, cancellationToken)
            : null;

        return Result<AdminProfileResponse>.Success(
            AdminProfileMapper.ToResponse(account, currentUser, organization));
    }
}
