using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Moe.Modules.FasPayment.Domain.Fas;

namespace Moe.Modules.FasPayment.Infrastructure.Persistence;

internal sealed class FasRuleConfiguration : IEntityTypeConfiguration<FasRule>
{
    public void Configure(EntityTypeBuilder<FasRule> builder)
    {
        builder.ToTable("FASRule", "fas");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("FASRuleId").UseIdentityColumn();
        builder.Property(x => x.FasTierId).HasColumnName("FASTierId");
        builder.HasIndex(x => x.FasTierId);
        builder.Property(x => x.CriterionCode).HasMaxLength(50).IsUnicode(false).IsRequired();
        builder.Property(x => x.OperatorCode).HasMaxLength(30).IsUnicode(false).IsRequired();
        builder.Property(x => x.NumericValueFrom).HasPrecision(19, 2);
        builder.Property(x => x.NumericValueTo).HasPrecision(19, 2);
        builder.Property(x => x.TextValue).HasMaxLength(300);
    }
}
