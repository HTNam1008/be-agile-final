using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Moe.Modules.Notifications.Domain.Notifications;

namespace Moe.Modules.Notifications.Infrastructure.Persistence;

internal sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("Notification", "communication");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("NotificationId")
            .UseIdentityColumn();

        builder.Property(x => x.RecipientUserAccountId)
            .HasColumnName("RecipientUserAccountId")
            .IsRequired();

        builder.Property(x => x.NotificationTypeCode)
            .HasMaxLength(50)
            .IsUnicode(false)
            .IsRequired();

        builder.Property(x => x.ReferenceTypeCode)
            .HasColumnName("ReferenceTypeCode")
            .HasMaxLength(50)
            .IsUnicode(false)
            .IsRequired();

        builder.Property(x => x.ChannelCode)
            .HasColumnName("ChannelCode")
            .HasMaxLength(50)
            .IsUnicode(false)
            .IsRequired();

        builder.Property(x => x.TemplateCode)
            .HasColumnName("TemplateCode")
            .HasMaxLength(100)
            .IsUnicode(false)
            .IsRequired();

        builder.Property(x => x.Title)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.Body)
            .HasMaxLength(4000)
            .IsRequired();

        builder.Property(x => x.CreatedAtUtc)
            .HasColumnName("CreatedAt")
            .IsRequired();

        builder.Property(x => x.ReadAtUtc)
            .HasColumnName("ReadAt");

        builder.Property(x => x.NotificationStatusCode)
            .HasMaxLength(30)
            .IsUnicode(false)
            .IsRequired();

        builder.HasIndex(x => new { x.RecipientUserAccountId, x.ReadAtUtc });
        builder.HasIndex(x => new { x.RecipientUserAccountId, x.CreatedAtUtc });
    }
}
