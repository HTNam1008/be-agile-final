using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Moe.Modules.FasPayment.Domain.Fas;

namespace Moe.Modules.FasPayment.Infrastructure.Persistence;

internal sealed class FasTierCriteriaConfiguration : IEntityTypeConfiguration<FasTierCriteria>
{
    public void Configure(EntityTypeBuilder<FasTierCriteria> builder)
    {
        builder.ToTable("FASTierCriteria", "fas", table =>
        {
            table.HasCheckConstraint("CK_FASTierCriteria_Type", "[CriteriaType] IN ('AGE','GDP','GHI','PCI','NATIONALITY','PARENT_NATIONALITY','ACCOUNT_TYPE')");
            table.HasCheckConstraint("CK_FASTierCriteria_Connector", "[ConnectorToNext] IS NULL OR [ConnectorToNext] IN ('AND','OR')");
            table.HasCheckConstraint("CK_FASTierCriteria_Range", "([CriteriaType] IN ('NATIONALITY','PARENT_NATIONALITY','ACCOUNT_TYPE') AND [NumberFrom] IS NULL AND [NumberTo] IS NULL) OR ([CriteriaType] NOT IN ('NATIONALITY','PARENT_NATIONALITY','ACCOUNT_TYPE') AND [NumberFrom] IS NOT NULL AND [NumberTo] IS NOT NULL AND [NumberFrom] <= [NumberTo])");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("FASTierCriteriaId").UseIdentityColumn();
        builder.Property(x => x.FasTierId).HasColumnName("FASTierId");
        builder.Property(x => x.CriteriaType).HasMaxLength(30).IsUnicode(false).IsRequired();
        builder.Property(x => x.NumberFrom).HasPrecision(18, 2);
        builder.Property(x => x.NumberTo).HasPrecision(18, 2);
        builder.Property(x => x.ConnectorToNext).HasMaxLength(10).IsUnicode(false);
        builder.Property(x => x.CreatedAtUtc).HasColumnName("CreatedAt");
        builder.Property(x => x.UpdatedAtUtc).HasColumnName("UpdatedAt");
        builder.HasIndex(x => new { x.FasTierId, x.DisplayOrder }).IsUnique();
        builder.HasOne<FasTier>().WithMany().HasForeignKey(x => x.FasTierId).OnDelete(DeleteBehavior.Cascade);
    }
}
