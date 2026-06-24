using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Moe.Modules.FasPayment.Domain.Fas;

namespace Moe.Modules.FasPayment.Infrastructure.Persistence;

internal sealed class FasTierCriteriaNationalityConfiguration : IEntityTypeConfiguration<FasTierCriteriaNationality>
{
    public void Configure(EntityTypeBuilder<FasTierCriteriaNationality> builder)
    {
        builder.ToTable("FASTierCriteriaNationality", "fas");
        builder.HasKey(x => new { x.FasTierCriteriaId, x.Nationality });
        builder.Property(x => x.FasTierCriteriaId).HasColumnName("FASTierCriteriaId");
        builder.Property(x => x.Nationality).HasMaxLength(100).IsRequired();
        builder.HasOne<FasTierCriteria>().WithMany().HasForeignKey(x => x.FasTierCriteriaId).OnDelete(DeleteBehavior.Cascade);
    }
}
