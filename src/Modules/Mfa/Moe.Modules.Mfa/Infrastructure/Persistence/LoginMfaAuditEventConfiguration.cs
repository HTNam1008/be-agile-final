using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Moe.Modules.Mfa.Domain;

namespace Moe.Modules.Mfa.Infrastructure.Persistence;

internal sealed class LoginMfaAuditEventConfiguration : IEntityTypeConfiguration<LoginMfaAuditEvent>
{
    public void Configure(EntityTypeBuilder<LoginMfaAuditEvent> builder)
    {
        builder.ToTable("LoginMfaAuditEvent", "iam");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("LoginMfaAuditEventId").UseIdentityColumn();
        builder.HasIndex(x => new { x.LoginAccountId, x.CreatedAtUtc });
        builder.HasIndex(x => x.PerformedByAccountId);
        builder.Property(x => x.EventCode).HasMaxLength(60).IsUnicode(false).IsRequired();
        builder.Property(x => x.Reason).HasMaxLength(1000);
        builder.Property(x => x.IpAddress).HasMaxLength(64);
        builder.Property(x => x.UserAgent).HasMaxLength(512);
    }
}
