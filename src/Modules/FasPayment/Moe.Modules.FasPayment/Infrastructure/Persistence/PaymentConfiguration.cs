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
        builder.HasIndex(x => x.BillingStatementId);
        builder.HasIndex(x => x.ProviderPaymentIntentId).IsUnique().HasFilter("[ProviderPaymentIntentId] IS NOT NULL");
        builder.HasIndex(x => x.ProviderInvoiceId).IsUnique().HasFilter("[ProviderInvoiceId] IS NOT NULL");
        builder.HasIndex(x => x.ProviderChargeId).HasFilter("[ProviderChargeId] IS NOT NULL");
        builder.Property(x => x.PaymentNumber).HasMaxLength(50).IsRequired();
        builder.Property(x => x.PaymentAmount).HasPrecision(19, 2);
        builder.Property(x => x.SuccessfulAmount).HasPrecision(19, 2);
        builder.Property(x => x.PaymentStatusCode).HasMaxLength(30).IsUnicode(false).IsRequired();
        builder.Property(x => x.InitiatedAtUtc).HasColumnName("InitiatedAt");
        builder.Property(x => x.CompletedAtUtc).HasColumnName("CompletedAt");
        builder.Property(x => x.IdempotencyKey).HasMaxLength(120).IsRequired();
        builder.Property(x => x.ReceiptNumber).HasMaxLength(50);
        builder.Property(x => x.ProviderPaymentIntentId).HasMaxLength(100);
        builder.Property(x => x.ProviderInvoiceId).HasMaxLength(100);
        builder.Property(x => x.ProviderChargeId).HasMaxLength(100);
        builder.Property(x => x.EducationAccountAmount).HasPrecision(19, 2);
        builder.Property(x => x.OnlinePaymentAmount).HasPrecision(19, 2);
        builder.Property(x => x.PaymentModeCode).HasMaxLength(50).IsUnicode(false);
        builder.Property(x => x.FailedAtUtc).HasColumnName("FailedAt");
        builder.Property(x => x.ExpiredAtUtc).HasColumnName("ExpiredAt");
        builder.Property(x => x.UpdatedAtUtc).HasColumnName("UpdatedAt");
        builder.Property(x => x.RowVersion).IsRowVersion();
    }
}
