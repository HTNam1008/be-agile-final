using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Moe.Modules.IdentityPlatform.Domain.People;

namespace Moe.Modules.IdentityPlatform.Infrastructure.Persistence;

public sealed class PersonConfiguration : IEntityTypeConfiguration<Person>
{
    public void Configure(EntityTypeBuilder<Person> builder)
    {
        builder.ToTable("Person", "person");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("PersonId").UseIdentityColumn();
        builder.HasIndex(x => x.ExternalPersonReference).IsUnique();
        builder.Property(x => x.ExternalPersonReference).HasColumnName("MockPassPersonId").HasMaxLength(100).IsRequired();
        builder.Property(x => x.IdentityNumberMasked).HasMaxLength(30);
        builder.Property(x => x.OfficialFullName).HasColumnName("FullName").HasMaxLength(200).IsRequired();
        builder.Property(x => x.NationalityCode).HasMaxLength(30).IsUnicode(false).IsRequired();
        builder.Property(x => x.CitizenshipStatusCode).HasColumnName("ResidencyStatusCode").HasMaxLength(30).IsUnicode(false);
        builder.Property(x => x.OfficialEmail).HasMaxLength(320);
        builder.Property(x => x.PreferredEmail).HasMaxLength(320);
        builder.Property(x => x.OfficialMobile).HasMaxLength(50);
        builder.Property(x => x.PreferredMobile).HasMaxLength(50);
        builder.Property(x => x.OfficialAddress).HasMaxLength(1000);
        builder.Property(x => x.PreferredAddress).HasMaxLength(1000);
        builder.Property(x => x.PersonStatusCode).HasMaxLength(30).IsUnicode(false).IsRequired();
        builder.Property(x => x.SourceUpdatedAtUtc).HasColumnName("SourceUpdatedAt");
        builder.Property(x => x.CreatedAtUtc).HasColumnName("CreatedAt");
        builder.Property(x => x.UpdatedAtUtc).HasColumnName("UpdatedAt");
        builder.Property(x => x.RowVersion).IsRowVersion();
        builder.Ignore(x => x.DomainEvents);
        builder.HasData(DemoSeedData.MockPassStudents.Select(Seed));
    }

    private static object Seed(MockPassStudentSeed student)
        => new
        {
            Id = student.PersonId,
            ExternalPersonReference = student.SingpassSubjectId,
            IdentityNumberMasked = student.Nric,
            OfficialFullName = student.FullName,
            DateOfBirth = student.DateOfBirth,
            NationalityCode = student.Nric.StartsWith('F') ? "FOREIGN" : "SG",
            CitizenshipStatusCode = student.Nric.StartsWith('F') ? ResidencyStatusCodes.ValidPassHolder : ResidencyStatusCodes.Citizen,
            OfficialEmail = student.Email,
            PreferredEmail = student.Email,
            OfficialMobile = student.Mobile,
            PreferredMobile = student.Mobile,
            OfficialAddress = student.Address,
            PreferredAddress = student.Address,
            PersonStatusCode = "ACTIVE",
            SourceUpdatedAtUtc = (DateTime?)DemoSeedData.SeededAtUtc,
            CreatedAtUtc = DemoSeedData.SeededAtUtc,
            UpdatedAtUtc = DemoSeedData.SeededAtUtc
        };
}
