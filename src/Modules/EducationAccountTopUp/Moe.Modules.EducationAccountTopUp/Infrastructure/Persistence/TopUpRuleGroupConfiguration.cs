using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Moe.Modules.EducationAccountTopUp.Domain.TopUps;

namespace Moe.Modules.EducationAccountTopUp.Infrastructure.Persistence;

internal sealed class TopUpRuleGroupConfiguration : IEntityTypeConfiguration<TopUpRuleGroup>
{
    public void Configure(EntityTypeBuilder<TopUpRuleGroup> builder)
    {
        builder.ToTable("TopUpRuleGroup", "topup");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("TopUpRuleGroupId").UseIdentityColumn();
        builder.Property(x => x.DisplayOrder);
        builder.Property(x => x.CreatedAtUtc).HasColumnName("CreatedAt");
        builder.Property(x => x.UpdatedAtUtc).HasColumnName("UpdatedAt");
        builder.HasIndex(x => new { x.TopUpCampaignId, x.DisplayOrder }).IsUnique();
        builder.HasOne<TopUpCampaign>()
            .WithMany()
            .HasForeignKey(x => x.TopUpCampaignId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
