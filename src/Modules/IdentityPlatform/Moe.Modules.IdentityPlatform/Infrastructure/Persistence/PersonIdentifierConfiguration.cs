using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Moe.Modules.IdentityPlatform.Domain.People;

namespace Moe.Modules.IdentityPlatform.Infrastructure.Persistence;

internal sealed class PersonIdentifierConfiguration : IEntityTypeConfiguration<PersonIdentifier>
{
    public void Configure(EntityTypeBuilder<PersonIdentifier> builder)
    {
        builder.ToTable("PersonIdentifier", "person");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("PersonIdentifierId").UseIdentityColumn();
        builder.HasIndex(x => new { x.IdentifierTypeCode, x.IdentifierValueHash }).IsUnique();
        builder.HasIndex(x => new { x.PersonId, x.IdentifierTypeCode, x.IsPrimary })
            .IsUnique()
            .HasFilter("[IdentifierStatusCode] = 'ACTIVE' AND [IsPrimary] = 1");
        builder.Property(x => x.IdentifierTypeCode).HasMaxLength(50).IsUnicode(false).IsRequired();
        builder.Property(x => x.IdentifierValueEncrypted).HasColumnType("varbinary(max)");
        builder.Property(x => x.IdentifierValueHash).HasColumnType("binary(32)").IsRequired();
        builder.Property(x => x.IdentifierMasked).HasMaxLength(100);
        builder.Property(x => x.IssuingCountryCode).HasMaxLength(2).IsFixedLength();
        builder.Property(x => x.IssuedByAuthority).HasMaxLength(150);
        builder.Property(x => x.IdentifierStatusCode).HasMaxLength(30).IsUnicode(false).IsRequired();
        builder.Property(x => x.SourceSystemCode).HasMaxLength(50).IsUnicode(false).IsRequired();
        builder.Property(x => x.RowVersion).IsRowVersion();
        builder.Ignore(x => x.DomainEvents);
        builder.HasData(DemoSeedData.MockPassStudents.SelectMany(Seed));
    }

    private static IEnumerable<object> Seed(MockPassStudentSeed student)
    {
        long baseId = student.PersonId * 10;

        yield return new
        {
            Id = baseId + 1,
            PersonId = student.PersonId,
            IdentifierTypeCode = "SINGPASS_SUBJECT",
            IdentifierValueEncrypted = (byte[]?)null,
            IdentifierValueHash = Hash($"{DemoSeedData.MockPassIssuer}|{student.SingpassSubjectId}"),
            IdentifierMasked = student.SingpassSubjectId,
            IsPrimary = false,
            IssuingCountryCode = "SG",
            IssuedByAuthority = "MOCKPASS",
            IdentifierStatusCode = "ACTIVE",
            EffectiveFrom = new DateOnly(2026, 1, 1),
            EffectiveTo = (DateOnly?)null,
            SourceSystemCode = "MOCKPASS_DEMO",
            CreatedAtUtc = DemoSeedData.SeededAtUtc,
            UpdatedAtUtc = DemoSeedData.SeededAtUtc
        };

        yield return new
        {
            Id = baseId + 2,
            PersonId = student.PersonId,
            IdentifierTypeCode = "IDENTITY_NUMBER",
            IdentifierValueEncrypted = (byte[]?)null,
            IdentifierValueHash = Hash(student.Nric),
            IdentifierMasked = student.Nric,
            IsPrimary = true,
            IssuingCountryCode = "SG",
            IssuedByAuthority = "MOCKPASS",
            IdentifierStatusCode = "ACTIVE",
            EffectiveFrom = new DateOnly(2026, 1, 1),
            EffectiveTo = (DateOnly?)null,
            SourceSystemCode = "MOCKPASS_DEMO",
            CreatedAtUtc = DemoSeedData.SeededAtUtc,
            UpdatedAtUtc = DemoSeedData.SeededAtUtc
        };
    }

    private static byte[] Hash(string value)
        => SHA256.HashData(Encoding.UTF8.GetBytes(value.Trim().ToUpperInvariant()));
}
