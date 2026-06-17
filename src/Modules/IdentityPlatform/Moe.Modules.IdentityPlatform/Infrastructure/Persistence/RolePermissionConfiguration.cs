using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Moe.Modules.IdentityPlatform.Domain.Iam;

namespace Moe.Modules.IdentityPlatform.Infrastructure.Persistence;

internal sealed class RolePermissionConfiguration : IEntityTypeConfiguration<RolePermission>
{
    public void Configure(EntityTypeBuilder<RolePermission> builder)
    {
        builder.ToTable("RolePermission", "iam");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("RolePermissionId").UseIdentityColumn();
        builder.HasIndex(x => new { x.RoleCode, x.PermissionCode, x.EffectiveFromUtc }).IsUnique();
        builder.Property(x => x.RoleCode).HasMaxLength(50).IsUnicode(false).IsRequired();
        builder.Property(x => x.PermissionCode).HasMaxLength(100).IsUnicode(false).IsRequired();
        builder.Property(x => x.StatusCode).HasMaxLength(30).IsUnicode(false).IsRequired();
        builder.Property(x => x.RowVersion).IsRowVersion();
        DateTime effectiveFromUtc = new(2026, 1, 1);
        builder.HasData(
            Seed(1, RoleCodes.SystemAdmin, PermissionCodes.OrgViewAll, effectiveFromUtc),
            Seed(2, RoleCodes.SystemAdmin, PermissionCodes.SchoolStudentView, effectiveFromUtc),
            Seed(3, RoleCodes.SystemAdmin, PermissionCodes.SchoolAdminProvision, effectiveFromUtc),
            Seed(4, RoleCodes.SystemAdmin, PermissionCodes.LoginDisable, effectiveFromUtc),
            Seed(5, RoleCodes.SystemAdmin, PermissionCodes.ExternalAccountsProvision, effectiveFromUtc),
            Seed(6, RoleCodes.SystemAdmin, PermissionCodes.AccessScopeManage, effectiveFromUtc),
            Seed(7, RoleCodes.SystemAdmin, PermissionCodes.AccountsViewAll, effectiveFromUtc),
            Seed(8, RoleCodes.SystemAdmin, PermissionCodes.AccountsManualCreate, effectiveFromUtc),
            Seed(9, RoleCodes.SystemAdmin, PermissionCodes.AccountsLifecycleManage, effectiveFromUtc),
            Seed(10, RoleCodes.SystemAdmin, PermissionCodes.AccountsSettlementView, effectiveFromUtc),
            Seed(11, RoleCodes.SystemAdmin, PermissionCodes.TopUpsViewAll, effectiveFromUtc),
            Seed(12, RoleCodes.SystemAdmin, PermissionCodes.CourseViewAll, effectiveFromUtc),
            Seed(13, RoleCodes.SystemAdmin, PermissionCodes.CourseDisableAny, effectiveFromUtc),
            Seed(14, RoleCodes.SystemAdmin, PermissionCodes.FasSchemeManage, effectiveFromUtc),
            Seed(15, RoleCodes.SystemAdmin, PermissionCodes.FasReview, effectiveFromUtc),
            Seed(16, RoleCodes.SystemAdmin, PermissionCodes.PaymentExceptionsReview, effectiveFromUtc),
            Seed(17, RoleCodes.SystemAdmin, PermissionCodes.AuditViewAll, effectiveFromUtc),
            Seed(18, RoleCodes.SystemAdmin, PermissionCodes.ReportExportAll, effectiveFromUtc),
            Seed(19, RoleCodes.SchoolAdmin, PermissionCodes.SchoolStudentView, effectiveFromUtc),
            Seed(20, RoleCodes.SchoolAdmin, PermissionCodes.AccountsViewSchool, effectiveFromUtc),
            Seed(21, RoleCodes.SchoolAdmin, PermissionCodes.CourseManageOwnSchool, effectiveFromUtc),
            Seed(22, RoleCodes.SchoolAdmin, PermissionCodes.CourseFeeManageOwnSchool, effectiveFromUtc),
            Seed(23, RoleCodes.SchoolAdmin, PermissionCodes.CourseAssignStudent, effectiveFromUtc),
            Seed(24, RoleCodes.SchoolAdmin, PermissionCodes.AuditViewSchool, effectiveFromUtc),
            Seed(25, RoleCodes.SchoolAdmin, PermissionCodes.ReportExportSchool, effectiveFromUtc),
            Seed(26, RoleCodes.Student, PermissionCodes.StudentAccountViewSelf, effectiveFromUtc));
    }

    private static object Seed(long id, string roleCode, string permissionCode, DateTime effectiveFromUtc)
        => new
        {
            Id = id,
            RoleCode = roleCode,
            PermissionCode = permissionCode,
            StatusCode = "ACTIVE",
            EffectiveFromUtc = effectiveFromUtc,
            EffectiveToUtc = (DateTime?)null
        };
}
