using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Moe.Modules.IdentityPlatform.Domain.Iam;

namespace Moe.Modules.IdentityPlatform.Infrastructure.Persistence;

internal sealed class UserAccountConfiguration : IEntityTypeConfiguration<UserAccount>
{
    public void Configure(EntityTypeBuilder<UserAccount> builder)
    {
        builder.ToTable("LoginAccount", "iam");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("LoginAccountId").UseIdentityColumn();
        builder.HasIndex(x => new { x.IdentityProviderCode, x.ExternalIssuer, x.ExternalSubjectId }).IsUnique();
        builder.HasIndex(x => new { x.IdentityProviderCode, x.ExternalTenantId, x.ExternalObjectId })
            .IsUnique()
            .HasFilter("[ExternalObjectId] IS NOT NULL");
        builder.HasIndex(x => x.PersonId);
        builder.HasIndex(x => x.AdminOrganizationId);
        builder.HasIndex(x => x.LoginEmailNormalized);
        builder.Property(x => x.AdminOrganizationId).HasColumnName("AdminOrganizationId");
        builder.Property(x => x.RoleCode).HasMaxLength(40).IsUnicode(false).IsRequired();
        builder.Property(x => x.IdentityProviderCode).HasMaxLength(40).IsUnicode(false).IsRequired();
        builder.Property(x => x.ExternalTenantId).HasMaxLength(100);
        builder.Property(x => x.ExternalIssuer).HasMaxLength(300).IsRequired();
        builder.Property(x => x.ExternalSubjectId).HasMaxLength(200).IsRequired();
        builder.Property(x => x.ExternalObjectId).HasMaxLength(100);
        builder.Property(x => x.ProviderDisplayName).HasMaxLength(200);
        builder.Property(x => x.ProviderLoginName).HasMaxLength(320);
        builder.Property(x => x.ProviderEmail).HasMaxLength(320);
        builder.Property(x => x.ProviderMobile).HasMaxLength(50);
        builder.Property(x => x.ContactEmail).HasMaxLength(320);
        builder.Property(x => x.ContactMobile).HasMaxLength(50);
        builder.Property(x => x.LoginEmailNormalized).HasMaxLength(320);
        builder.Property(x => x.DisplayNameSnapshot).HasMaxLength(200);
        builder.Property(x => x.UserTypeCode).HasMaxLength(30).IsUnicode(false).IsRequired();
        builder.Property(x => x.PortalAccessCode).HasMaxLength(30).IsUnicode(false).IsRequired();
        builder.Property(x => x.AccountStatusCode).HasColumnName("LoginStatusCode").HasMaxLength(40).IsUnicode(false).IsRequired();
        builder.Property(x => x.CreatedByUserAccountId).HasColumnName("CreatedByLoginAccountId");
        builder.Property(x => x.CreatedAtUtc).HasColumnName("CreatedAt");
        builder.Property(x => x.UpdatedAtUtc).HasColumnName("UpdatedAt");
        builder.Property(x => x.LastSyncedAtUtc).HasColumnName("LastSyncedAt");
        builder.Property(x => x.LastLoginAtUtc).HasColumnName("LastLoginAt");
        builder.Property(x => x.RowVersion).IsRowVersion();
        builder.Ignore(x => x.DomainEvents);
        object[] adminAccounts =
        [
            SeedAdmin(
                DemoSeedData.HqAdminLoginAccountId,
                OrganizationUnitCodes.MoeHeadquartersId,
                RoleCodes.HqAdmin,
                DemoSeedData.HqAdminObjectId,
                "system.admin@moe.local",
                "MOE HQ Admin",
                createdByLoginAccountId: null),
            SeedAdmin(
                DemoSeedData.SchoolAdminLoginAccountId,
                OrganizationUnitCodes.DemoSchoolId,
                RoleCodes.SchoolAdmin,
                DemoSeedData.SchoolAdminObjectId,
                "school.admin@demo-school.local",
                "Demo School Admin",
                DemoSeedData.HqAdminLoginAccountId)
        ];

        builder.HasData(adminAccounts.Concat(DemoSeedData.MockPassStudents.Select(SeedStudent)));
    }

    private static object SeedAdmin(
        long id,
        long organizationId,
        string roleCode,
        string objectId,
        string email,
        string displayName,
        long? createdByLoginAccountId)
        => new
        {
            Id = id,
            PersonId = (long?)null,
            AdminOrganizationId = (long?)organizationId,
            RoleCode = roleCode,
            IdentityProviderCode = IdentityProviderCodes.EntraWorkforce,
            ExternalTenantId = DemoSeedData.TenantId,
            ExternalIssuer = DemoSeedData.EntraIssuer,
            ExternalSubjectId = objectId,
            ExternalObjectId = objectId,
            ProviderDisplayName = displayName,
            ProviderLoginName = email,
            ProviderEmail = email,
            ProviderMobile = (string?)null,
            ContactEmail = email,
            ContactMobile = (string?)null,
            LoginEmailNormalized = email.ToUpperInvariant(),
            DisplayNameSnapshot = displayName,
            UserTypeCode = UserTypeCodes.Internal,
            PortalAccessCode = PortalAccessCodes.Admin,
            AccountStatusCode = UserAccountStatusCodes.Active,
            FirstLoginAtUtc = (DateTime?)null,
            LastLoginAtUtc = (DateTime?)null,
            LastSyncedAtUtc = DemoSeedData.SeededAtUtc,
            CreatedAtUtc = DemoSeedData.SeededAtUtc,
            CreatedByUserAccountId = createdByLoginAccountId,
            UpdatedAtUtc = DemoSeedData.SeededAtUtc
        };

    private static object SeedStudent(MockPassStudentSeed student)
        => new
        {
            Id = student.LoginAccountId,
            PersonId = (long?)student.PersonId,
            AdminOrganizationId = (long?)null,
            RoleCode = RoleCodes.Student,
            IdentityProviderCode = IdentityProviderCodes.Singpass,
            ExternalTenantId = (string?)null,
            ExternalIssuer = DemoSeedData.MockPassIssuer,
            ExternalSubjectId = student.SingpassSubjectId,
            ExternalObjectId = (string?)null,
            ProviderDisplayName = student.FullName,
            ProviderLoginName = student.Nric,
            ProviderEmail = (string?)null,
            ProviderMobile = (string?)null,
            ContactEmail = student.Email,
            ContactMobile = student.Mobile,
            LoginEmailNormalized = (string?)null,
            DisplayNameSnapshot = student.FullName,
            UserTypeCode = UserTypeCodes.EService,
            PortalAccessCode = PortalAccessCodes.EService,
            AccountStatusCode = UserAccountStatusCodes.Active,
            FirstLoginAtUtc = (DateTime?)null,
            LastLoginAtUtc = (DateTime?)null,
            LastSyncedAtUtc = DemoSeedData.SeededAtUtc,
            CreatedAtUtc = DemoSeedData.SeededAtUtc,
            CreatedByUserAccountId = (long?)DemoSeedData.HqAdminLoginAccountId,
            UpdatedAtUtc = DemoSeedData.SeededAtUtc
        };
}
