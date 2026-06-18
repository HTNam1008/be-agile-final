using Moe.Application.Abstractions.Security;
using Moe.Modules.IdentityPlatform.Domain.Iam;

namespace Moe.Modules.IdentityPlatform.Application.AdminProfile;

internal static class AdminProfileMapper
{
    public static AdminProfileResponse ToResponse(UserAccount account, ICurrentUser currentUser)
    {
        return new AdminProfileResponse(
            account.Id,
            account.DisplayNameSnapshot ?? account.ProviderDisplayName ?? string.Empty,
            account.LoginEmailNormalized,
            account.ContactEmail,
            account.ContactMobile,
            account.RoleCode,
            account.AdminOrganizationId,
            account.IdentityProviderCode,
            account.ExternalTenantId,
            account.ExternalIssuer,
            account.ExternalSubjectId,
            account.ExternalObjectId,
            account.ProviderDisplayName,
            account.ProviderLoginName,
            account.ProviderEmail,
            account.AccountStatusCode,
            account.FirstLoginAtUtc,
            account.LastLoginAtUtc,
            account.CreatedAtUtc,
            account.UpdatedAtUtc,
            currentUser.OrganizationUnitIds,
            currentUser.Roles,
            currentUser.Permissions);
    }
}
