using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Moe.Modules.FasPayment.Domain.Fas;

namespace Moe.Modules.FasPayment.Infrastructure.Persistence;

internal sealed class FasTierConfiguration : IEntityTypeConfiguration<FasTier>
{
    public void Configure(EntityTypeBuilder<FasTier> builder)
    {
        builder.ToTable("FASTier", "fas");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("FASTierId").UseIdentityColumn();
        builder.Property(x => x.FasSchemeId).HasColumnName("FASSchemeId");
        builder.HasIndex(x => new { x.FasSchemeId, x.TierCode }).IsUnique();
        builder.Property(x => x.TierCode).HasMaxLength(50).IsRequired();
        builder.Property(x => x.TierName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.StatusCode).HasMaxLength(30).IsUnicode(false).IsRequired();
    }
}
