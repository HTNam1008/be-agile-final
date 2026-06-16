using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Moe.Modules.FasPayment.Domain.Fas;

namespace Moe.Modules.FasPayment.Infrastructure.Persistence;

internal sealed class FasTierBenefitConfiguration : IEntityTypeConfiguration<FasTierBenefit>
{
    public void Configure(EntityTypeBuilder<FasTierBenefit> builder)
    {
        builder.ToTable("FASTierBenefit", "fas");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("FASTierBenefitId").UseIdentityColumn();
        builder.Property(x => x.FasTierId).HasColumnName("FASTierId");
        builder.HasIndex(x => new { x.FasTierId, x.FeeComponentId });
        builder.Property(x => x.SubsidyTypeCode).HasMaxLength(30).IsUnicode(false).IsRequired();
        builder.Property(x => x.SubsidyValue).HasPrecision(19, 4);
        builder.Property(x => x.MaximumSubsidyAmount).HasPrecision(19, 2);
    }
}
