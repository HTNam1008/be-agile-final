namespace Moe.Infrastructure.Shared.Security;

public static class AuthenticationSchemes
{
    public const string AdminEntra = "AdminEntra";
    public const string EServiceSingpass = "EServiceSingpass";
}

public static class AuthenticationCookies
{
    public const string AdminSession = "moe_admin_session";
    public const string EServiceSession = "moe_eservice_session";
    public const string EServiceSingpassLoginSession = "moe_eservice_singpass_login_session";
}

public static class AuthorizationPolicies
{
    public const string AdminPortal = "AdminPortal";
    public const string EServicePortal = "EServicePortal";
    public const string ManageAccessScopes = "ManageAccessScopes";
    public const string ManageAccounts = "ManageAccounts";
    public const string ViewAccountDetails = "ViewAccountDetails";
    public const string ManageAccountDetails = "ManageAccountDetails";
    public const string ManageAccountLifecycle = "ManageAccountLifeCycle";
    public const string LifecycleManualTrigger = "LifecycleManualTrigger";
    public const string ManageExternalAccounts = "ManageExternalAccounts";
    public const string ManageTopUps = "ManageTopUps";
    public const string ViewTopUps = "ViewTopUps";
    public const string ManageCourses = "ManageCourses";
    public const string ReviewFas = "ReviewFas";
    public const string ManageFasSchemes = "ManageFasSchemes";
}

public static class LocalIdentityClaimNames
{
    public const string ExternalAuthenticationScheme = "external_authentication_scheme";
    public const string UserAccountId = "user_account_id";
    public const string PersonId = "person_id";
    public const string OrganizationUnitId = "organization_unit_id";
    public const string Role = "role";
    public const string Portal = "portal";
    public const string Permission = "permission";
    public const string IdentityProvider = "identity_provider";
}

public static class ClaimNames
{
    public const string UserAccountId = LocalIdentityClaimNames.UserAccountId;
    public const string PersonId = LocalIdentityClaimNames.PersonId;
    public const string OrganizationUnitId = LocalIdentityClaimNames.OrganizationUnitId;
    public const string Role = LocalIdentityClaimNames.Role;
    public const string Portal = LocalIdentityClaimNames.Portal;
    public const string Permission = LocalIdentityClaimNames.Permission;
    public const string IdentityProvider = LocalIdentityClaimNames.IdentityProvider;
}

public static class PortalCodes
{
    public const string Admin = "ADMIN";
    public const string EService = "ESERVICE";
}
