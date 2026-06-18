using Moe.SharedKernel.Results;

namespace Moe.Modules.IdentityPlatform.Application;

internal static class IdentityErrors
{
    public static readonly Error AuthenticatedAdminRequired = new("IDENTITY.AUTHENTICATED_ADMIN_REQUIRED", "An authenticated admin account is required.");
    public static readonly Error AuthenticatedUserRequired = new("IDENTITY.AUTHENTICATED_USER_REQUIRED", "An authenticated user account is required.");
    public static readonly Error PersonNotFound = new("IDENTITY.PERSON_NOT_FOUND", "The person was not found.");
    public static readonly Error UserAccountNotFound = new("IDENTITY.USER_ACCOUNT_NOT_FOUND", "The user account was not found.");
    public static readonly Error ProvisioningRequestNotFound = new("IDENTITY.PROVISIONING_REQUEST_NOT_FOUND", "The provisioning request was not found.");
    public static readonly Error AdminAccountAlreadyExists = new("IDENTITY.ADMIN_ACCOUNT_ALREADY_EXISTS", "An admin account already exists for this user principal name.");
    public static readonly Error AdminCannotCreateOwnAccount = new("IDENTITY.ADMIN_CANNOT_CREATE_OWN_ACCOUNT", "An admin cannot create their own admin account.");
    public static readonly Error OrganizationUnitNotFound = new("IDENTITY.ORGANIZATION_UNIT_NOT_FOUND", "The organization unit was not found or is not active.");
    public static readonly Error RoleNotConfigured = new("IDENTITY.ROLE_NOT_CONFIGURED", "The requested role is not configured with active permissions.");
    public static readonly Error SystemAdminRequired = new("IDENTITY.SYSTEM_ADMIN_REQUIRED", "Only a system admin can provision admin accounts.");
    public static readonly Error InvalidAdminRole = new("IDENTITY.INVALID_ADMIN_ROLE", "Admin role must be SYSTEM_ADMIN or SCHOOL_ADMIN.");
    public static readonly Error InvalidAdminOrganizationScope = new("IDENTITY.INVALID_ADMIN_ORGANIZATION_SCOPE", "The requested admin role is not valid for the selected organization scope.");
    public static readonly Error ActiveAccessScopeAlreadyExists = new("IDENTITY.ACTIVE_ACCESS_SCOPE_ALREADY_EXISTS", "The user already has this active access scope.");
    public static readonly Error SingpassAccountAlreadyExists = new("IDENTITY.SINGPASS_ACCOUNT_ALREADY_EXISTS", "A Singpass account is already linked to this person.");
    public static readonly Error ActiveProvisioningRequestAlreadyExists = new("IDENTITY.ACTIVE_PROVISIONING_REQUEST_ALREADY_EXISTS", "An active provisioning request already exists.");

    public static Error AdminDirectoryCreateFailed(string reason)
        => new("IDENTITY.ADMIN_DIRECTORY_CREATE_FAILED", $"Microsoft Entra ID could not create the admin user. {reason}");

    public static Error AdminLocalAccountCreateFailed(string reason)
        => new("IDENTITY.ADMIN_LOCAL_ACCOUNT_CREATE_FAILED", $"Microsoft Entra ID created the user, but the local MOE admin account could not be saved. {reason}");
}
