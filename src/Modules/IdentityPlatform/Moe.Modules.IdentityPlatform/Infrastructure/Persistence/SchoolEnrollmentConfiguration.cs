using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Moe.Modules.IdentityPlatform.Domain.Iam;
using Moe.Modules.IdentityPlatform.Domain.Schooling;

namespace Moe.Modules.IdentityPlatform.Infrastructure.Persistence;

internal sealed class SchoolEnrollmentConfiguration : IEntityTypeConfiguration<SchoolEnrollment>
{
    public void Configure(EntityTypeBuilder<SchoolEnrollment> builder)
    {
        builder.ToTable("SchoolEnrollment", "person");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("SchoolEnrollmentId").UseIdentityColumn();
        builder.HasIndex(x => new { x.PersonId, x.OrganizationId, x.AcademicYear });
        builder.HasIndex(x => x.StudentNumber);
        builder.Property(x => x.StudentNumber).HasMaxLength(50).IsRequired();
        builder.Property(x => x.AcademicYear).HasMaxLength(20).IsUnicode(false).IsRequired();
        builder.Property(x => x.LevelCode).HasMaxLength(30).IsUnicode(false).IsRequired();
        builder.Property(x => x.ClassCode).HasMaxLength(30).IsUnicode(false).IsRequired();
        builder.Property(x => x.SchoolingStatusCode).HasMaxLength(30).IsUnicode(false).IsRequired();
        builder.Property(x => x.StatusReasonCode).HasMaxLength(50).IsUnicode(false);
        builder.Property(x => x.SourceCode).HasMaxLength(50).IsUnicode(false).IsRequired();
        builder.Property(x => x.CreatedAtUtc).HasColumnName("CreatedAt");
        builder.Property(x => x.UpdatedAtUtc).HasColumnName("UpdatedAt");
        builder.HasData(
            Seed(DemoSeedData.StudentSchoolEnrollmentId, DemoSeedData.StudentPersonId, "DEMO-STU-0001", "SEC_4", "4A", "ACTIVE"),
            Seed(DemoSeedData.TopUpStudentSchoolEnrollmentIdOne, DemoSeedData.TopUpStudentPersonIdOne, "DEMO-STU-0002", "SEC_3", "3B", "ACTIVE"),
            Seed(DemoSeedData.TopUpStudentSchoolEnrollmentIdTwo, DemoSeedData.TopUpStudentPersonIdTwo, "DEMO-STU-0003", "SEC_2", "2C", "ACTIVE"),
            Seed(DemoSeedData.TopUpStudentSchoolEnrollmentIdThree, DemoSeedData.TopUpStudentPersonIdThree, "DEMO-STU-0004", "SEC_4", "4A", "ON_LEAVE"));
    }

    private static object Seed(
        long id,
        long personId,
        string studentNumber,
        string levelCode,
        string classCode,
        string schoolingStatusCode)
        => new
        {
            Id = id,
            PersonId = personId,
            OrganizationId = OrganizationUnitCodes.DemoSchoolId,
            StudentNumber = studentNumber,
            AcademicYear = "2026",
            LevelCode = levelCode,
            ClassCode = classCode,
            SchoolingStatusCode = schoolingStatusCode,
            StatusReasonCode = (string?)null,
            StartDate = new DateOnly(2026, 1, 2),
            EndDate = (DateOnly?)null,
            SourceCode = "DEMO_SEED",
            CreatedAtUtc = DemoSeedData.SeededAtUtc,
            UpdatedAtUtc = DemoSeedData.SeededAtUtc
        };
}
