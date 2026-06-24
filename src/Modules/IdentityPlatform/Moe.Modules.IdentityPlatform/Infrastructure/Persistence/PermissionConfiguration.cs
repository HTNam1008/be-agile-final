using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Moe.Modules.IdentityPlatform.Domain.Iam;

namespace Moe.Modules.IdentityPlatform.Infrastructure.Persistence;

internal sealed class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
    public void Configure(EntityTypeBuilder<Permission> builder)
    {
        builder.ToTable("Permission", "iam");
        builder.HasKey(x => x.PermissionCode);
        builder.Property(x => x.PermissionCode).HasMaxLength(100).IsUnicode(false);
        builder.Property(x => x.PermissionName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.ModuleCode).HasMaxLength(50).IsUnicode(false).IsRequired();
        builder.Property(x => x.ActionCode).HasMaxLength(30).IsUnicode(false).IsRequired();
        builder.Property(x => x.ResourceCode).HasMaxLength(50).IsUnicode(false).IsRequired();
        builder.Property(x => x.StatusCode).HasMaxLength(30).IsUnicode(false).IsRequired();
        builder.HasData(
            new Permission(PermissionCodes.OrgViewAll, "View all organizations", "IDENTITY_PLATFORM", "VIEW", "ORGANIZATIONS"),
            new Permission(PermissionCodes.SchoolStudentView, "View assigned school students", "IDENTITY_PLATFORM", "VIEW", "SCHOOL_STUDENTS"),
            new Permission(PermissionCodes.SchoolAdminProvision, "Provision school admin accounts", "IDENTITY_PLATFORM", "PROVISION", "SCHOOL_ADMINS"),
            new Permission(PermissionCodes.LoginDisable, "Disable permitted login accounts", "IDENTITY_PLATFORM", "DISABLE", "LOGIN_ACCOUNTS"),
            new Permission(PermissionCodes.AccountsViewAll, "View all education accounts", "EDUCATION_ACCOUNT_TOPUP", "VIEW", "ACCOUNTS_ALL"),
            new Permission(PermissionCodes.AccountsViewSchool, "View own-school education account summaries", "EDUCATION_ACCOUNT_TOPUP", "VIEW", "ACCOUNTS_SCHOOL"),
            new Permission(PermissionCodes.StudentAccountViewSelf, "View own education account", "EDUCATION_ACCOUNT_TOPUP", "VIEW", "ACCOUNT_SELF"),
            new Permission(PermissionCodes.AccountsManualCreate, "Manually create education accounts", "EDUCATION_ACCOUNT_TOPUP", "CREATE", "ACCOUNTS"),
            new Permission(PermissionCodes.AccountDetailsManage, "Manage education account details", "EDUCATION_ACCOUNT_TOPUP", "MANAGE", "ACCOUNT_DETAILS"),
            new Permission(PermissionCodes.AccountsLifecycleManage, "Suspend, reactivate and close education accounts", "EDUCATION_ACCOUNT_TOPUP", "MANAGE", "ACCOUNT_LIFECYCLE"),
            new Permission(PermissionCodes.LifecycleManualTrigger, "Trigger education account lifecycle manually", "EDUCATION_ACCOUNT_TOPUP", "TRIGGER", "ACCOUNT_LIFECYCLE"),
            new Permission(PermissionCodes.AccountsSettlementView, "View settlement operations", "EDUCATION_ACCOUNT_TOPUP", "VIEW", "SETTLEMENTS"),
            new Permission(PermissionCodes.ExternalAccountsProvision, "Create admin users and prepare student Singpass access", "IDENTITY_PLATFORM", "PROVISION", "EXTERNAL_ACCOUNTS"),
            new Permission(PermissionCodes.AccessScopeManage, "Manage access scopes", "IDENTITY_PLATFORM", "MANAGE", "ACCESS_SCOPE"),
            new Permission(PermissionCodes.TopUpsViewAll, "View national top-up activity", "EDUCATION_ACCOUNT_TOPUP", "VIEW", "TOPUPS_ALL"),
            new Permission(PermissionCodes.TopUpsManage, "Manage top-ups", "EDUCATION_ACCOUNT_TOPUP", "MANAGE", "TOPUPS"),
            new Permission(PermissionCodes.CourseViewAll, "View all courses", "COURSE_BILLING", "VIEW", "COURSES_ALL"),
            new Permission(PermissionCodes.CourseManageAny, "Manage courses across any school", "COURSE_BILLING", "MANAGE", "COURSES_ANY"),
            new Permission(PermissionCodes.CourseDisableAny, "Disable any course with reason", "COURSE_BILLING", "DISABLE", "COURSES_ANY"),
            new Permission(PermissionCodes.CourseManageOwnSchool, "Manage own-school courses", "COURSE_BILLING", "MANAGE", "COURSES_OWN_SCHOOL"),
            new Permission(PermissionCodes.CourseFeeManageOwnSchool, "Manage own-school course fees", "COURSE_BILLING", "MANAGE", "COURSE_FEES_OWN_SCHOOL"),
            new Permission(PermissionCodes.CourseAssignStudent, "Assign own-school students to courses", "COURSE_BILLING", "ASSIGN", "COURSE_STUDENTS"),
            new Permission(PermissionCodes.FasReview, "Review FAS applications", "FAS_PAYMENT", "REVIEW", "FAS"),
            new Permission(PermissionCodes.FasSchemeManage, "Manage national FAS schemes", "FAS_PAYMENT", "MANAGE", "FAS_SCHEMES"),
            new Permission(PermissionCodes.PaymentExceptionsReview, "Review payment exceptions", "FAS_PAYMENT", "REVIEW", "PAYMENT_EXCEPTIONS"),
            new Permission(PermissionCodes.AuditViewAll, "View national audit", "IDENTITY_PLATFORM", "VIEW", "AUDIT_ALL"),
            new Permission(PermissionCodes.ReportExportAll, "Export national reports", "IDENTITY_PLATFORM", "EXPORT", "REPORTS_ALL"),
            new Permission(PermissionCodes.AuditViewSchool, "View own-school audit", "IDENTITY_PLATFORM", "VIEW", "AUDIT_SCHOOL"),
            new Permission(PermissionCodes.ReportExportSchool, "Export own-school reports", "IDENTITY_PLATFORM", "EXPORT", "REPORTS_SCHOOL"));
    }
}
