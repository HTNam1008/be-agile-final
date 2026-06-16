using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Moe.Modules.FasPayment.Domain.Payments;

namespace Moe.Modules.FasPayment.Infrastructure.Persistence;

internal sealed class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("Payment", "payment");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("PaymentId").UseIdentityColumn();
        builder.HasIndex(x => x.PaymentNumber).IsUnique();
        builder.HasIndex(x => x.IdempotencyKey).IsUnique();
        builder.HasIndex(x => x.BillId);
        builder.Property(x => x.PaymentNumber).HasMaxLength(50).IsRequired();
        builder.Property(x => x.PaymentAmount).HasPrecision(19, 2);
        builder.Property(x => x.SuccessfulAmount).HasPrecision(19, 2);
        builder.Property(x => x.PaymentStatusCode).HasMaxLength(30).IsUnicode(false).IsRequired();
        builder.Property(x => x.InitiatedAtUtc).HasColumnName("InitiatedAt");
        builder.Property(x => x.CompletedAtUtc).HasColumnName("CompletedAt");
        builder.Property(x => x.IdempotencyKey).HasMaxLength(120).IsRequired();
        builder.Property(x => x.ReceiptNumber).HasMaxLength(50);
    }
}
