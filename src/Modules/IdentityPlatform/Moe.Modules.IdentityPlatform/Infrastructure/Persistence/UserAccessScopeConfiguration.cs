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
        object[] adminScopes =
        [
            Seed(1001, DemoSeedData.HqAdminLoginAccountId, OrganizationUnitCodes.MoeHeadquartersId, RoleCodes.HqAdmin, null),
            Seed(1002, DemoSeedData.SchoolAdminLoginAccountId, OrganizationUnitCodes.DemoSchoolId, RoleCodes.SchoolAdmin, DemoSeedData.HqAdminLoginAccountId)
        ];

        builder.HasData(adminScopes.Concat(DemoSeedData.MockPassStudents.Select(SeedStudent)));
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

    private static object SeedStudent(MockPassStudentSeed student)
        => Seed(
            student.UserAccessScopeId,
            student.LoginAccountId,
            OrganizationUnitCodes.DemoSchoolId,
            RoleCodes.Student,
            DemoSeedData.HqAdminLoginAccountId);
}
