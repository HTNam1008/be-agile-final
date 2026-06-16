namespace Moe.Modules.IdentityPlatform.Domain.Iam;

internal static class OrganizationUnitCodes
{
    public const long MoeHeadquartersId = 1;
    public const string MoeHeadquarters = "MOE_HQ";
    public const long DemoSchoolId = 2;
    public const string DemoSchool = "DEMO_SCHOOL";
}

internal static class IamStatusCodes
{
    public const string Active = "ACTIVE";
    public const string Revoked = "REVOKED";
}

internal static class PortalAccessCodes
{
    public const string Admin = "ADMIN";
    public const string EService = "ESERVICE";
}

internal static class UserTypeCodes
{
    public const string Internal = "INTERNAL";
    public const string EService = "ESERVICE";
}

internal static class RoleCodes
{
    public const string SystemAdmin = "SYSTEM_ADMIN";
    public const string SchoolAdmin = "SCHOOL_ADMIN";
    public const string Student = "STUDENT";

    public const string Admin = SystemAdmin;
}

internal static class PermissionCodes
{
    public const string AccountsView = "ACCOUNTS_VIEW";
    public const string AccountsManage = "ACCOUNTS_MANAGE";
    public const string ExternalAccountsProvision = "EXTERNAL_ACCOUNTS_PROVISION";
    public const string AccessScopeManage = "ACCESS_SCOPE_MANAGE";
    public const string TopUpsManage = "TOPUPS_MANAGE";
    public const string CoursesManage = "COURSES_MANAGE";
    public const string FasReview = "FAS_REVIEW";
    public const string PaymentExceptionsReview = "PAYMENT_EXCEPTIONS_REVIEW";
}
