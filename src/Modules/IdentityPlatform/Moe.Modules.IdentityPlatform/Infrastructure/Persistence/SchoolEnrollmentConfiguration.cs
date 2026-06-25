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
        builder.Property(x => x.ClassCode).HasMaxLength(30).IsUnicode(false);
        builder.Property(x => x.SchoolingStatusCode).HasMaxLength(30).IsUnicode(false).IsRequired();
        builder.Property(x => x.StatusReasonCode).HasMaxLength(50).IsUnicode(false);
        builder.Property(x => x.SourceCode).HasMaxLength(50).IsUnicode(false).IsRequired();
        builder.Property(x => x.CreatedAtUtc).HasColumnName("CreatedAt");
        builder.Property(x => x.UpdatedAtUtc).HasColumnName("UpdatedAt");
        builder.HasData(DemoSeedData.MockPassStudents.Select(Seed));
    }

    private static object Seed(MockPassStudentSeed student)
        => new
        {
            Id = student.EnrollmentId,
            PersonId = student.PersonId,
            OrganizationId = student.OrganizationId,
            StudentNumber = student.StudentNumber,
            AcademicYear = "2026",
            LevelCode = student.LevelCode,
            ClassCode = student.ClassCode,
            SchoolingStatusCode = "ACTIVE",
            StatusReasonCode = (string?)null,
            StartDate = new DateOnly(2026, 1, 2),
            EndDate = (DateOnly?)null,
            SourceCode = "DEMO_SEED",
            CreatedAtUtc = DemoSeedData.SeededAtUtc,
            UpdatedAtUtc = DemoSeedData.SeededAtUtc
        };
}
