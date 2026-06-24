using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Moe.Modules.FasPayment.Domain.Payments;

namespace Moe.Modules.FasPayment.Infrastructure.Persistence;

internal sealed class PaymentAllocationConfiguration : IEntityTypeConfiguration<PaymentAllocation>
{
    public void Configure(EntityTypeBuilder<PaymentAllocation> builder)
    {
        builder.ToTable("PaymentAllocation", "payment");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("PaymentAllocationId").UseIdentityColumn();
        builder.HasIndex(x => new { x.PaymentId, x.BillId }).IsUnique();
        builder.Property(x => x.AllocatedAmount).HasPrecision(19, 2);
        builder.Property(x => x.AllocationStatusCode).HasMaxLength(30).IsUnicode(false);
        builder.Property(x => x.CreatedAtUtc).HasColumnName("CreatedAt");
    }
}
