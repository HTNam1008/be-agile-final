using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Moe.Modules.IdentityPlatform.Domain.Notifications;

namespace Moe.Modules.IdentityPlatform.Infrastructure.Persistence;

internal sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("Notification", "communication");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("NotificationId").UseIdentityColumn();
        builder.HasIndex(x => x.RecipientPersonId);
        builder.HasIndex(x => x.RecipientLoginAccountId);
        builder.Property(x => x.NotificationTypeCode).HasMaxLength(50).IsUnicode(false).IsRequired();
        builder.Property(x => x.ReferenceTypeCode).HasMaxLength(50).IsUnicode(false).IsRequired();
        builder.Property(x => x.ChannelCode).HasMaxLength(30).IsUnicode(false).IsRequired();
        builder.Property(x => x.TemplateCode).HasMaxLength(100).IsUnicode(false).IsRequired();
        builder.Property(x => x.NotificationStatusCode).HasMaxLength(30).IsUnicode(false).IsRequired();
        builder.Property(x => x.CreatedAtUtc).HasColumnName("CreatedAt");
        builder.Property(x => x.SentAtUtc).HasColumnName("SentAt");
        builder.Property(x => x.FailureReason).HasMaxLength(1000);
    }
}
