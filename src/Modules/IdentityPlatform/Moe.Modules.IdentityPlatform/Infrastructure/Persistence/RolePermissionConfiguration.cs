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
            Seed(1, RoleCodes.SystemAdmin, PermissionCodes.AccessScopeManage, effectiveFromUtc),
            Seed(2, RoleCodes.SystemAdmin, PermissionCodes.ExternalAccountsProvision, effectiveFromUtc),
            Seed(3, RoleCodes.SystemAdmin, PermissionCodes.AccountsView, effectiveFromUtc),
            Seed(4, RoleCodes.SystemAdmin, PermissionCodes.AccountsManage, effectiveFromUtc),
            Seed(5, RoleCodes.SystemAdmin, PermissionCodes.TopUpsManage, effectiveFromUtc),
            Seed(6, RoleCodes.SystemAdmin, PermissionCodes.CoursesManage, effectiveFromUtc),
            Seed(7, RoleCodes.SystemAdmin, PermissionCodes.FasReview, effectiveFromUtc),
            Seed(8, RoleCodes.SystemAdmin, PermissionCodes.PaymentExceptionsReview, effectiveFromUtc),
            Seed(9, RoleCodes.SchoolAdmin, PermissionCodes.AccountsView, effectiveFromUtc),
            Seed(10, RoleCodes.SchoolAdmin, PermissionCodes.CoursesManage, effectiveFromUtc),
            Seed(11, RoleCodes.SchoolAdmin, PermissionCodes.FasReview, effectiveFromUtc),
            Seed(12, RoleCodes.Student, PermissionCodes.AccountsView, effectiveFromUtc));
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
