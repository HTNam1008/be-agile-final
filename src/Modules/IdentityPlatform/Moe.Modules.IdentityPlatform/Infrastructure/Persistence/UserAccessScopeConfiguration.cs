using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Moe.Modules.IdentityPlatform.Domain.Iam;

namespace Moe.Modules.IdentityPlatform.Infrastructure.Persistence;

internal sealed class UserAccessScopeConfiguration : IEntityTypeConfiguration<UserAccessScope>
{
    public void Configure(EntityTypeBuilder<UserAccessScope> builder)
    {
        builder.ToTable("UserAccessScope", "iam");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("UserAccessScopeId").UseIdentityColumn();
        builder.HasIndex(x => new { x.UserAccountId, x.OrganizationUnitId, x.RoleCode, x.EffectiveFromUtc }).IsUnique();
        builder.Property(x => x.RoleCode).HasMaxLength(50).IsUnicode(false).IsRequired();
        builder.Property(x => x.StatusCode).HasMaxLength(30).IsUnicode(false).IsRequired();
        builder.Property(x => x.RowVersion).IsRowVersion();
        builder.Ignore(x => x.DomainEvents);
        builder.HasData(
            Seed(1001, DemoSeedData.SystemAdminLoginAccountId, OrganizationUnitCodes.MoeHeadquartersId, RoleCodes.SystemAdmin, null),
            Seed(1002, DemoSeedData.SchoolAdminLoginAccountId, OrganizationUnitCodes.DemoSchoolId, RoleCodes.SchoolAdmin, DemoSeedData.SystemAdminLoginAccountId),
            Seed(1003, DemoSeedData.StudentLoginAccountId, OrganizationUnitCodes.DemoSchoolId, RoleCodes.Student, DemoSeedData.SystemAdminLoginAccountId));
    }

    private static object Seed(long id, long loginAccountId, long organizationId, string roleCode, long? createdByLoginAccountId)
        => new
        {
            Id = id,
            UserAccountId = loginAccountId,
            OrganizationUnitId = organizationId,
            RoleCode = roleCode,
            StatusCode = IamStatusCodes.Active,
            EffectiveFromUtc = DemoSeedData.SeededAtUtc,
            EffectiveToUtc = (DateTime?)null,
            CreatedAtUtc = DemoSeedData.SeededAtUtc,
            CreatedByUserAccountId = createdByLoginAccountId
        };
}
