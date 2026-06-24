using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Moe.Modules.Mfa.Domain;

namespace Moe.Modules.Mfa.Infrastructure.Persistence;

internal sealed class LoginMfaChallengeConfiguration : IEntityTypeConfiguration<LoginMfaChallenge>
{
    public void Configure(EntityTypeBuilder<LoginMfaChallenge> builder)
    {
        builder.ToTable("LoginMfaChallenge", "iam");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("LoginMfaChallengeId");
        builder.HasIndex(x => new { x.LoginAccountId, x.StatusCode, x.ExpiresAtUtc });
        builder.Property(x => x.PurposeCode).HasMaxLength(30).IsUnicode(false).IsRequired();
        builder.Property(x => x.StatusCode).HasMaxLength(30).IsUnicode(false).IsRequired();
        builder.Property(x => x.FailedAttemptCount).HasDefaultValue(0);
    }
}
