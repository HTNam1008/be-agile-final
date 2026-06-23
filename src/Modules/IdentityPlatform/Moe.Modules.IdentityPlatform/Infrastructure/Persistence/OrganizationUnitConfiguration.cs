using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Moe.Modules.IdentityPlatform.Domain.Iam;

namespace Moe.Modules.IdentityPlatform.Infrastructure.Persistence;

internal sealed class OrganizationUnitConfiguration : IEntityTypeConfiguration<OrganizationUnit>
{
    public void Configure(EntityTypeBuilder<OrganizationUnit> builder)
    {
        builder.ToTable("Organization", "org");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("OrganizationId").UseIdentityColumn();
        builder.Property(x => x.ParentOrganizationUnitId).HasColumnName("ParentOrganizationId");
        builder.HasIndex(x => x.UnitCode).IsUnique();
        builder.HasIndex(x => x.MockPassSchoolCode).HasFilter("[MockPassSchoolCode] IS NOT NULL");
        builder.Property(x => x.UnitCode).HasColumnName("OrganizationCode").HasMaxLength(50).IsRequired();
        builder.Property(x => x.UnitName).HasColumnName("OrganizationName").HasMaxLength(200).IsRequired();
        builder.Property(x => x.UnitTypeCode).HasColumnName("OrganizationTypeCode").HasMaxLength(30).IsUnicode(false).IsRequired();
        builder.Property(x => x.MockPassSchoolCode).HasMaxLength(50);
        builder.Property(x => x.StatusCode).HasColumnName("OrganizationStatusCode").HasMaxLength(30).IsUnicode(false).IsRequired();
        builder.Property(x => x.CreatedAtUtc).HasColumnName("CreatedAt");
        builder.Property(x => x.UpdatedAtUtc).HasColumnName("UpdatedAt");
        builder.Property(x => x.RowVersion).IsRowVersion();
        builder.Ignore(x => x.DomainEvents);
        builder.ToTable(x => x.HasCheckConstraint("CK_Organization_Parent_NotSelf", "[ParentOrganizationId] IS NULL OR [ParentOrganizationId] <> [OrganizationId]"));
        object headquarters = new
        {
            Id = OrganizationUnitCodes.MoeHeadquartersId,
            ParentOrganizationUnitId = (long?)null,
            UnitCode = OrganizationUnitCodes.MoeHeadquarters,
            UnitName = "Ministry of Education Headquarters",
            UnitTypeCode = "HQ",
            MockPassSchoolCode = (string?)null,
            StatusCode = "ACTIVE",
            CreatedAtUtc = DemoSeedData.SeededAtUtc,
            UpdatedAtUtc = DemoSeedData.SeededAtUtc,
            EffectiveFromUtc = DemoSeedData.SeededAtUtc,
            EffectiveToUtc = (DateTime?)null
        };

        builder.HasData(new[] { headquarters }.Concat(DemoSeedData.MockSchools.Select(SeedSchool)));
    }

    private static object SeedSchool(MockSchoolSeed school)
        => new
        {
            Id = school.OrganizationId,
            ParentOrganizationUnitId = (long?)OrganizationUnitCodes.MoeHeadquartersId,
            UnitCode = school.SchoolCode,
            UnitName = school.SchoolName,
            UnitTypeCode = "SCHOOL",
            MockPassSchoolCode = school.MockPassSchoolCode,
            StatusCode = "ACTIVE",
            CreatedAtUtc = DemoSeedData.SeededAtUtc,
            UpdatedAtUtc = DemoSeedData.SeededAtUtc,
            EffectiveFromUtc = DemoSeedData.SeededAtUtc,
            EffectiveToUtc = (DateTime?)null
        };
}
