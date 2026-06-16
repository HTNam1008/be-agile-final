using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Moe.Modules.FasPayment.Domain.Fas;

namespace Moe.Modules.FasPayment.Infrastructure.Persistence;

internal sealed class FasSubsidyConfiguration : IEntityTypeConfiguration<FasSubsidy>
{
    public void Configure(EntityTypeBuilder<FasSubsidy> builder)
    {
        builder.ToTable("FASSubsidy", "fas");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("FASSubsidyId").UseIdentityColumn();
        builder.HasIndex(x => x.FasApplicationId);
        builder.HasIndex(x => x.BillLineId);
        builder.Property(x => x.FasApplicationId).HasColumnName("FASApplicationId");
        builder.Property(x => x.FasTierBenefitId).HasColumnName("FASTierBenefitId");
        builder.Property(x => x.GrossAmountSnapshot).HasPrecision(19, 2);
        builder.Property(x => x.CalculatedAmount).HasPrecision(19, 2);
        builder.Property(x => x.AppliedAmount).HasPrecision(19, 2);
        builder.Property(x => x.SubsidyStatusCode).HasMaxLength(30).IsUnicode(false).IsRequired();
        builder.Property(x => x.AppliedAtUtc).HasColumnName("AppliedAt");
    }
}
