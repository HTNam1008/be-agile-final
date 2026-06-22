namespace Moe.Modules.IdentityPlatform.Domain.Iam;

internal static class OrganizationUnitCodes
{
    public const long MoeHeadquartersId = 1;
    public const string MoeHeadquarters = "MOE_HQ";
    public const long NusId = 2;
    public const long NtuId = 3;
    public const long SmuId = 4;
    public const long SutdId = 5;
    public const long SitId = 6;
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
    public const string HqAdmin = "HQ_ADMIN";
    public const string SchoolAdmin = "SCHOOL_ADMIN";
    public const string Student = "STUDENT";
}

internal static class PermissionCodes
{
    public const string OrgViewAll = "ORG_VIEW_ALL";
    public const string SchoolStudentView = "SCHOOL_STUDENT_VIEW";
    public const string SchoolAdminProvision = "SCHOOL_ADMIN_PROVISION";
    public const string LoginDisable = "LOGIN_DISABLE";
    public const string AccountsViewAll = "ACCOUNT_VIEW_ALL";
    public const string AccountsViewSchool = "ACCOUNT_VIEW_SCHOOL";
    public const string StudentAccountViewSelf = "STUDENT_ACCOUNT_VIEW_SELF";
    public const string AccountsManualCreate = "ACCOUNT_MANUAL_CREATE";
    public const string AccountDetailsManage = "ACCOUNT_DETAILS_MANAGE";
    public const string AccountsLifecycleManage = "ACCOUNT_LIFECYCLE_MANAGE";
    public const string AccountsSettlementView = "ACCOUNT_SETTLEMENT_VIEW";
    public const string ExternalAccountsProvision = "EXTERNAL_ACCOUNTS_PROVISION";
    public const string AccessScopeManage = "ACCESS_SCOPE_MANAGE";
    public const string TopUpsViewAll = "TOPUP_VIEW_ALL";
    public const string TopUpsManage = "TOPUPS_MANAGE";
    public const string CourseViewAll = "COURSE_VIEW_ALL";
    public const string CourseManageAny = "COURSE_MANAGE_ANY";
    public const string CourseDisableAny = "COURSE_DISABLE_ANY";
    public const string CourseManageOwnSchool = "COURSE_MANAGE_OWN_SCHOOL";
    public const string CourseFeeManageOwnSchool = "COURSE_FEE_MANAGE_OWN_SCHOOL";
    public const string CourseAssignStudent = "COURSE_ASSIGN_STUDENT";
    public const string FasReview = "FAS_REVIEW";
    public const string FasSchemeManage = "FAS_SCHEME_MANAGE";
    public const string PaymentExceptionsReview = "PAYMENT_EXCEPTIONS_REVIEW";
    public const string AuditViewAll = "AUDIT_VIEW_ALL";
    public const string ReportExportAll = "REPORT_EXPORT_ALL";
    public const string AuditViewSchool = "AUDIT_VIEW_SCHOOL";
    public const string ReportExportSchool = "REPORT_EXPORT_SCHOOL";

    public const string AccountsView = AccountsViewAll;
    public const string AccountsManage = AccountsManualCreate;
    public const string CoursesManage = CourseManageOwnSchool;
}
