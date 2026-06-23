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
    public static readonly Error HqAdminRequired = new("IDENTITY.HQ_ADMIN_REQUIRED", "Only a HQ admin can provision admin accounts.");
    public static readonly Error InvalidAdminRole = new("IDENTITY.INVALID_ADMIN_ROLE", "Admin role must be HQ_ADMIN or SCHOOL_ADMIN.");
    public static readonly Error InvalidAdminOrganizationScope = new("IDENTITY.INVALID_ADMIN_ORGANIZATION_SCOPE", "The requested admin role is not valid for the selected organization scope.");
    public static readonly Error ActiveAccessScopeAlreadyExists = new("IDENTITY.ACTIVE_ACCESS_SCOPE_ALREADY_EXISTS", "The user already has this active access scope.");
    public static readonly Error SingpassAccountAlreadyExists = new("IDENTITY.SINGPASS_ACCOUNT_ALREADY_EXISTS", "A Singpass account is already linked to this person.");
    public static readonly Error ActiveProvisioningRequestAlreadyExists = new("IDENTITY.ACTIVE_PROVISIONING_REQUEST_ALREADY_EXISTS", "An active provisioning request already exists.");
    public static readonly Error ProfileUpdateConflict = new("IDENTITY.PROFILE_UPDATE_CONFLICT", "The profile was updated by another request. Reload the latest profile before saving changes.");
    public static readonly Error EducationAccountNotFound = new("IDENTITY.EDUCATION_ACCOUNT_NOT_FOUND", "The education account was not found.");
    public static readonly Error ActiveSchoolEnrollmentRequired = new("IDENTITY.ACTIVE_SCHOOL_ENROLLMENT_REQUIRED", "An active school enrollment is required to update class details.");
    public static readonly Error OrganizationOutsideScope = new("AUTH.ORGANIZATION_OUTSIDE_SCOPE", "The requested organization is outside the current admin's scope.");
    public static readonly Error SchoolRequired = new("IDENTITY.SCHOOL_REQUIRED", "A school is required when it cannot be resolved from the current admin scope.");
    public static readonly Error SchoolNameRequired = new("IDENTITY.SCHOOL_NAME_REQUIRED", "A school name is required when the school cannot be resolved from the current admin scope.");
    public static readonly Error SchoolScopeAmbiguous = new("IDENTITY.SCHOOL_SCOPE_AMBIGUOUS", "The current school admin has more than one school scope. Provide the school name.");
    public static readonly Error SchoolIdentifiersConflict = new("IDENTITY.SCHOOL_IDENTIFIERS_CONFLICT", "The provided school identifiers refer to different schools.");
    public static readonly Error SchoolOutsideScope = new("IDENTITY.SCHOOL_OUTSIDE_SCOPE", "The requested school is outside the current admin scope.");
    public static readonly Error StudentIdentityAlreadyExists = new("IDENTITY.STUDENT_IDENTITY_ALREADY_EXISTS", "A student already exists with this identity number.");
    public static readonly Error StudentNumberAlreadyExists = new("IDENTITY.STUDENT_NUMBER_ALREADY_EXISTS", "A student already exists with this student number.");
    public static readonly Error StudentAccountCreateFailed = new("IDENTITY.STUDENT_ACCOUNT_CREATE_FAILED", "The student was created, but the education account could not be created.");

    public static Error AdminDirectoryCreateFailed(string reason)
        => new("IDENTITY.ADMIN_DIRECTORY_CREATE_FAILED", $"Microsoft Entra ID could not create the admin user. {reason}");

    public static Error AdminLocalAccountCreateFailed(string reason)
        => new("IDENTITY.ADMIN_LOCAL_ACCOUNT_CREATE_FAILED", $"Microsoft Entra ID created the user, but the local MOE admin account could not be saved. {reason}");
}
