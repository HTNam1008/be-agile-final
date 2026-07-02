using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Moe.Modules.MailDelivery.Domain;

namespace Moe.Modules.MailDelivery.Infrastructure.Persistence;

internal sealed class EmailNotificationConfiguration : IEntityTypeConfiguration<EmailNotification>
{
    public void Configure(EntityTypeBuilder<EmailNotification> builder)
    {
        builder.ToTable("EmailNotification", "mail");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("EmailNotificationId").UseIdentityColumn();
        builder.Property(x => x.NotificationType).HasMaxLength(100).IsUnicode(false).IsRequired();
        builder.Property(x => x.Subject).HasMaxLength(500).IsRequired();
        builder.Property(x => x.PlainTextBody).IsRequired();
        builder.Property(x => x.EntityType).HasMaxLength(100).IsUnicode(false);
        builder.Property(x => x.EntityId).HasMaxLength(100).IsUnicode(false);
        builder.Property(x => x.StatusCode).HasMaxLength(30).IsUnicode(false).IsRequired();
        builder.Property(x => x.LastErrorCode).HasMaxLength(100).IsUnicode(false);
        builder.Property(x => x.LastErrorMessage).HasMaxLength(1000);
        builder.Property(x => x.ResolvedToEmailMasked).HasMaxLength(320);
        builder.Property(x => x.RecipientSourceCode).HasMaxLength(50).IsUnicode(false);

        builder.HasIndex(x => new { x.StatusCode, x.NextAttemptAtUtc, x.Priority, x.CreatedAtUtc })
            .HasDatabaseName("IX_EmailNotification_Queue");
        builder.HasIndex(x => new { x.NotificationType, x.PersonId, x.EntityType, x.EntityId })
            .HasDatabaseName("IX_EmailNotification_Dedupe");
        builder.HasIndex(x => x.CreatedAtUtc);
    }
}
