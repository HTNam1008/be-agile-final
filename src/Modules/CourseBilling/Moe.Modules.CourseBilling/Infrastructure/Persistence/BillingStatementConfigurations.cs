using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Moe.Modules.CourseBilling.Domain.Billing;

namespace Moe.Modules.CourseBilling.Infrastructure.Persistence;

internal sealed class BillingStatementConfiguration : IEntityTypeConfiguration<BillingStatement>
{
    public void Configure(EntityTypeBuilder<BillingStatement> builder)
    {
        builder.ToTable("BillingStatement", "billing");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("BillingStatementId").UseIdentityColumn();
        builder.HasIndex(x => new { x.PersonId, x.StatementYear, x.StatementMonth }).IsUnique();
        builder.Property(x => x.CurrencyCode).HasMaxLength(3).IsUnicode(false);
        builder.Property(x => x.TotalAmount).HasPrecision(19, 2);
        builder.Property(x => x.PaidAmount).HasPrecision(19, 2);
        builder.Property(x => x.OutstandingAmount).HasPrecision(19, 2);
        builder.Property(x => x.StatementStatusCode).HasMaxLength(30).IsUnicode(false);
        builder.Property(x => x.CreatedAtUtc).HasColumnName("CreatedAt");
        builder.Property(x => x.UpdatedAtUtc).HasColumnName("UpdatedAt");
        builder.Property(x => x.RowVersion).IsRowVersion();
    }
}

internal sealed class BillingStatementItemConfiguration : IEntityTypeConfiguration<BillingStatementItem>
{
    public void Configure(EntityTypeBuilder<BillingStatementItem> builder)
    {
        builder.ToTable("BillingStatementItem", "billing");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("BillingStatementItemId").UseIdentityColumn();
        builder.HasIndex(x => new { x.BillingStatementId, x.BillId }).IsUnique();
        builder.Property(x => x.IncludedAmount).HasPrecision(19, 2);
        builder.Property(x => x.PaidAmount).HasPrecision(19, 2);
        builder.Property(x => x.ItemStatusCode).HasMaxLength(30).IsUnicode(false);
        builder.Property(x => x.CreatedAtUtc).HasColumnName("CreatedAt");
    }
}

internal sealed class BillDeferralConfiguration : IEntityTypeConfiguration<BillDeferral>
{
    public void Configure(EntityTypeBuilder<BillDeferral> builder)
    {
        builder.ToTable("BillDeferral", "billing");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("BillDeferralId").UseIdentityColumn();
        builder.HasIndex(x => new { x.BillId, x.SourcePaymentId });
        builder.HasIndex(x => new { x.BillId, x.DeferralSequenceNumber }).IsUnique();
        builder.Property(x => x.DeferredAmount).HasPrecision(19, 2);
        builder.Property(x => x.ReasonCode).HasMaxLength(50).IsUnicode(false);
        builder.Property(x => x.CreatedAtUtc).HasColumnName("CreatedAt");
    }
}
