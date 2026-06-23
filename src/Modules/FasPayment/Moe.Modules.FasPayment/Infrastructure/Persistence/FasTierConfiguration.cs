using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Moe.Modules.FasPayment.Domain.Fas;

namespace Moe.Modules.FasPayment.Infrastructure.Persistence;

internal sealed class FasTierConfiguration : IEntityTypeConfiguration<FasTier>
{
    public void Configure(EntityTypeBuilder<FasTier> builder)
    {
        builder.ToTable("FASTier", "fas", table =>
        {
            table.HasCheckConstraint("CK_FASTier_SubsidyType", "[SubsidyType] IN ('FIXED','PERCENTAGE')");
            table.HasCheckConstraint("CK_FASTier_SubsidyValue", "[SubsidyValue] >= 0 AND ([SubsidyType] <> 'PERCENTAGE' OR [SubsidyValue] <= 100)");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("FASTierId").UseIdentityColumn();
        builder.Property(x => x.FasSchemeId).HasColumnName("FASSchemeId");
        builder.Property(x => x.Label).HasMaxLength(255).IsRequired();
        builder.Property(x => x.SubsidyType).HasMaxLength(30).IsUnicode(false).IsRequired();
        builder.Property(x => x.SubsidyValue).HasPrecision(18, 2);
        builder.Property(x => x.CreatedAtUtc).HasColumnName("CreatedAt");
        builder.Property(x => x.UpdatedAtUtc).HasColumnName("UpdatedAt");
        builder.HasIndex(x => new { x.FasSchemeId, x.DisplayOrder }).IsUnique();
        builder.HasOne<FasScheme>().WithMany().HasForeignKey(x => x.FasSchemeId).OnDelete(DeleteBehavior.Cascade);
    }
}
