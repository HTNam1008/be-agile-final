using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Moe.Modules.IdentityPlatform.Domain.Audit;

namespace Moe.Modules.IdentityPlatform.Infrastructure.Persistence;

internal sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("AuditLog", "audit");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("AuditLogId").UseIdentityColumn();
        builder.HasIndex(x => new { x.AuditScopeCode, x.OccurredAtUtc });
        builder.HasIndex(x => x.CorrelationId);
        builder.Property(x => x.AuditScopeCode).HasMaxLength(50).IsUnicode(false).IsRequired();
        builder.Property(x => x.ActorTypeCode).HasMaxLength(50).IsUnicode(false).IsRequired();
        builder.Property(x => x.ActorNameSnapshot).HasMaxLength(200);
        builder.Property(x => x.ActionCode).HasMaxLength(100).IsUnicode(false).IsRequired();
        builder.Property(x => x.EntityTypeCode).HasMaxLength(100).IsUnicode(false).IsRequired();
        builder.Property(x => x.OutcomeCode).HasMaxLength(50).IsUnicode(false).IsRequired();
        builder.Property(x => x.Reason).HasMaxLength(1000);
        builder.Property(x => x.ChangedFieldsJson).HasColumnType("nvarchar(max)");
        builder.Property(x => x.CorrelationId).HasMaxLength(100);
        builder.Property(x => x.OccurredAtUtc).HasColumnName("OccurredAt");
        builder.Property(x => x.IpAddress).HasMaxLength(100);
    }
}
