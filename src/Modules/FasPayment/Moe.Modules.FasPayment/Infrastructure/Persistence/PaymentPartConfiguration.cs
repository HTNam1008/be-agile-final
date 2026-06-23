using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Moe.Modules.FasPayment.Domain.Payments;

namespace Moe.Modules.FasPayment.Infrastructure.Persistence;

internal sealed class PaymentPartConfiguration : IEntityTypeConfiguration<PaymentPart>
{
    public void Configure(EntityTypeBuilder<PaymentPart> builder)
    {
        builder.ToTable("PaymentPart", "payment");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("PaymentPartId").UseIdentityColumn();
        builder.HasIndex(x => new { x.PaymentId, x.SequenceNumber }).IsUnique();
        builder.Property(x => x.PaymentMethodCode).HasMaxLength(30).IsUnicode(false).IsRequired();
        builder.Property(x => x.PartAmount).HasPrecision(19, 2);
        builder.Property(x => x.ProviderCode).HasMaxLength(50).IsUnicode(false);
        builder.Property(x => x.ProviderReference).HasMaxLength(150);
        builder.Property(x => x.PartStatusCode).HasMaxLength(30).IsUnicode(false).IsRequired();
        builder.Property(x => x.AuthorizedAtUtc).HasColumnName("AuthorizedAt");
        builder.Property(x => x.SettledAtUtc).HasColumnName("SettledAt");
        builder.Property(x => x.FailureReason).HasMaxLength(1000);
        builder.Property(x => x.CreatedAtUtc).HasColumnName("CreatedAt");
        builder.Property(x => x.CompletedAtUtc).HasColumnName("CompletedAt");
    }
}
