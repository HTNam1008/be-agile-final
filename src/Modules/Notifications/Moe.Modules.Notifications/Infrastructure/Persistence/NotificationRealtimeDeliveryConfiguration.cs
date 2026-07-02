using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Moe.Modules.Notifications.Domain.Notifications;

namespace Moe.Modules.Notifications.Infrastructure.Persistence;

internal sealed class NotificationRealtimeDeliveryConfiguration : IEntityTypeConfiguration<NotificationRealtimeDelivery>
{
    public void Configure(EntityTypeBuilder<NotificationRealtimeDelivery> builder)
    {
        builder.ToTable("NotificationRealtimeDelivery", "communication");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("NotificationRealtimeDeliveryId")
            .UseIdentityColumn();

        builder.Property(x => x.NotificationId)
            .HasColumnName("NotificationId")
            .IsRequired();

        builder.Property(x => x.RecipientUserAccountId)
            .HasColumnName("RecipientUserAccountId")
            .IsRequired();

        builder.Property(x => x.StatusCode)
            .HasMaxLength(30)
            .IsUnicode(false)
            .IsRequired();

        builder.Property(x => x.LastErrorCode)
            .HasMaxLength(100)
            .IsUnicode(false);

        builder.Property(x => x.LastErrorMessage)
            .HasMaxLength(1000);

        builder.HasOne(x => x.Notification)
            .WithMany()
            .HasForeignKey(x => x.NotificationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.StatusCode, x.NextAttemptAtUtc, x.CreatedAtUtc })
            .HasDatabaseName("IX_NotificationRealtimeDelivery_Queue");

        builder.HasIndex(x => x.NotificationId)
            .HasDatabaseName("IX_NotificationRealtimeDelivery_NotificationId");

        builder.HasIndex(x => new { x.RecipientUserAccountId, x.CreatedAtUtc })
            .HasDatabaseName("IX_NotificationRealtimeDelivery_Recipient_CreatedAt");
    }
}
