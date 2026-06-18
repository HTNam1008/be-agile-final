using Moe.Application.Abstractions.Messaging;
using Moe.Application.Abstractions.Security;
using Moe.Modules.IdentityPlatform.Application;
using Moe.Modules.IdentityPlatform.Domain.Iam;
using Moe.Modules.IdentityPlatform.IGateway.Repositories;
using Moe.SharedKernel.Results;

namespace Moe.Modules.IdentityPlatform.Application.AdminProfile.GetMyAdminProfile;

internal sealed class GetMyAdminProfileHandler(
    ICurrentUser currentUser,
    IUserAccountRepository userAccounts)
    : IQueryHandler<GetMyAdminProfileQuery, AdminProfileResponse>
{
    public async Task<Result<AdminProfileResponse>> Handle(GetMyAdminProfileQuery query, CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserAccountId is null || currentUser.Portal != PortalAccessCodes.Admin)
        {
            return Result<AdminProfileResponse>.Failure(IdentityErrors.AuthenticatedAdminRequired);
        }

        UserAccount? account = await userAccounts.FindByIdAsync(currentUser.UserAccountId.Value, cancellationToken);

        if (account is null || account.PortalAccessCode != PortalAccessCodes.Admin)
        {
            return Result<AdminProfileResponse>.Failure(IdentityErrors.UserAccountNotFound);
        }

        return Result<AdminProfileResponse>.Success(AdminProfileMapper.ToResponse(account, currentUser));
    }
}
