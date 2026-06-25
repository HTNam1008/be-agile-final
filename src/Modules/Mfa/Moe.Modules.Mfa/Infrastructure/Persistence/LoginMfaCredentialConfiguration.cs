using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Moe.Modules.Mfa.Domain;

namespace Moe.Modules.Mfa.Infrastructure.Persistence;

internal sealed class LoginMfaCredentialConfiguration : IEntityTypeConfiguration<LoginMfaCredential>
{
    public void Configure(EntityTypeBuilder<LoginMfaCredential> builder)
    {
        builder.ToTable("LoginMfaCredential", "iam");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("LoginMfaCredentialId").UseIdentityColumn();
        builder.HasIndex(x => new { x.LoginAccountId, x.MfaTypeCode }).IsUnique();
        builder.HasIndex(x => x.LoginAccountId);
        builder.Property(x => x.MfaTypeCode).HasMaxLength(30).IsUnicode(false).IsRequired();
        builder.Property(x => x.SecretHash).HasMaxLength(256).IsRequired();
        builder.Property(x => x.SecretSalt).HasMaxLength(32).IsRequired();
        builder.Property(x => x.SecretHashAlgorithm).HasMaxLength(50).IsUnicode(false).IsRequired();
        builder.Property(x => x.StatusCode).HasMaxLength(30).IsUnicode(false).IsRequired();
        builder.Property(x => x.FailedAttemptCount).HasDefaultValue(0);
        builder.Property(x => x.RowVersion).IsRowVersion();
    }
}
