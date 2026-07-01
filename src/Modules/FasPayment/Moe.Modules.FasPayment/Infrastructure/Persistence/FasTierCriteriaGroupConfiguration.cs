using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Moe.Modules.FasPayment.Domain.Fas;

namespace Moe.Modules.FasPayment.Infrastructure.Persistence;

internal sealed class FasTierCriteriaGroupConfiguration : IEntityTypeConfiguration<FasTierCriteriaGroup>
{
    public void Configure(EntityTypeBuilder<FasTierCriteriaGroup> builder)
    {
        builder.ToTable("FASTierCriteriaGroup", "fas");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("FASTierCriteriaGroupId").UseIdentityColumn();
        builder.Property(x => x.FasTierId).HasColumnName("FASTierId");
        builder.Property(x => x.CreatedAtUtc).HasColumnName("CreatedAt");
        builder.Property(x => x.UpdatedAtUtc).HasColumnName("UpdatedAt");
        builder.HasIndex(x => new { x.FasTierId, x.DisplayOrder }).IsUnique();
        builder.HasOne<FasTier>().WithMany().HasForeignKey(x => x.FasTierId).OnDelete(DeleteBehavior.Cascade);
    }
}
