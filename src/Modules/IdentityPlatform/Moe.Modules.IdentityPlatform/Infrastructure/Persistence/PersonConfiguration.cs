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
        builder.Property(x => x.CitizenshipStatusCode).HasColumnName("ResidencyStatusCode").HasMaxLength(30).IsUnicode(false).IsRequired();
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
        builder.HasData(
            Seed(
                DemoSeedData.StudentPersonId,
                DemoSeedData.MockPassSubject,
                DemoSeedData.MockPassNric,
                "Tan Mei Ling",
                DemoSeedData.StudentDateOfBirth,
                "student.official@example.test",
                "student@example.test",
                "+6590000001"),
            Seed(
                DemoSeedData.TopUpStudentPersonIdOne,
                "TOPUP-STUDENT-0002",
                "S234****B",
                "Aisha Rahman",
                new DateOnly(2009, 3, 18),
                "aisha.official@example.test",
                "aisha@example.test",
                "+6590000002"),
            Seed(
                DemoSeedData.TopUpStudentPersonIdTwo,
                "TOPUP-STUDENT-0003",
                "S345****C",
                "Brandon Lee",
                new DateOnly(2010, 9, 4),
                "brandon.official@example.test",
                "brandon@example.test",
                "+6590000003"),
            Seed(
                DemoSeedData.TopUpStudentPersonIdThree,
                "TOPUP-STUDENT-0004",
                "S456****D",
                "Chen Wei Jie",
                new DateOnly(2008, 11, 28),
                "weijie.official@example.test",
                "weijie@example.test",
                "+6590000004"));
    }

    private static object Seed(
        long id,
        string externalPersonReference,
        string identityNumberMasked,
        string fullName,
        DateOnly dateOfBirth,
        string officialEmail,
        string preferredEmail,
        string mobile)
        => new
        {
            Id = id,
            ExternalPersonReference = externalPersonReference,
            IdentityNumberMasked = identityNumberMasked,
            OfficialFullName = fullName,
            DateOfBirth = dateOfBirth,
            NationalityCode = "SG",
            CitizenshipStatusCode = "CITIZEN",
            OfficialEmail = officialEmail,
            PreferredEmail = preferredEmail,
            OfficialMobile = mobile,
            PreferredMobile = mobile,
            OfficialAddress = "1 Demo Street, Singapore 000001",
            PreferredAddress = "1 Demo Street, Singapore 000001",
            PersonStatusCode = "ACTIVE",
            SourceUpdatedAtUtc = (DateTime?)DemoSeedData.SeededAtUtc,
            CreatedAtUtc = DemoSeedData.SeededAtUtc,
            UpdatedAtUtc = DemoSeedData.SeededAtUtc
        };
}
