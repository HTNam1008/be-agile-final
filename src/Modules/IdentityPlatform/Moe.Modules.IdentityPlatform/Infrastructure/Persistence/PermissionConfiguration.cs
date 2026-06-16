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
            new Permission(PermissionCodes.AccountsView, "View accounts", "EDUCATION_ACCOUNT_TOPUP", "VIEW", "ACCOUNTS"),
            new Permission(PermissionCodes.AccountsManage, "Manage accounts", "EDUCATION_ACCOUNT_TOPUP", "MANAGE", "ACCOUNTS"),
            new Permission(PermissionCodes.ExternalAccountsProvision, "Create admin users and prepare student Singpass access", "IDENTITY_PLATFORM", "PROVISION", "EXTERNAL_ACCOUNTS"),
            new Permission(PermissionCodes.AccessScopeManage, "Manage access scopes", "IDENTITY_PLATFORM", "MANAGE", "ACCESS_SCOPE"),
            new Permission(PermissionCodes.TopUpsManage, "Manage top-ups", "EDUCATION_ACCOUNT_TOPUP", "MANAGE", "TOPUPS"),
            new Permission(PermissionCodes.CoursesManage, "Manage courses", "COURSE_BILLING", "MANAGE", "COURSES"),
            new Permission(PermissionCodes.FasReview, "Review FAS applications", "FAS_PAYMENT", "REVIEW", "FAS"),
            new Permission(PermissionCodes.PaymentExceptionsReview, "Review payment exceptions", "FAS_PAYMENT", "REVIEW", "PAYMENT_EXCEPTIONS"));
    }
}
