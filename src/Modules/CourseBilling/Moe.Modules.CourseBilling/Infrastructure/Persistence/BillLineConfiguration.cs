using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Moe.Modules.CourseBilling.Domain.Billing;

namespace Moe.Modules.CourseBilling.Infrastructure.Persistence;

internal sealed class BillLineConfiguration : IEntityTypeConfiguration<BillLine>
{
    public void Configure(EntityTypeBuilder<BillLine> builder)
    {
        builder.ToTable("BillLine", "billing");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("BillLineId").UseIdentityColumn();
        builder.HasIndex(x => x.BillId);
        builder.Property(x => x.DescriptionSnapshot).HasMaxLength(300).IsRequired();
        builder.Property(x => x.Quantity).HasPrecision(19, 4);
        builder.Property(x => x.UnitAmount).HasPrecision(19, 4);
        builder.Property(x => x.GrossAmount).HasPrecision(19, 2);
        builder.Property(x => x.SubsidyAmount).HasPrecision(19, 2);
        builder.Property(x => x.NetAmount).HasPrecision(19, 2);
    }
}
