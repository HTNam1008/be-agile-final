using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Moe.Modules.EducationAccountTopUp.Domain.TopUps;

namespace Moe.Modules.EducationAccountTopUp.Infrastructure.Persistence;

internal sealed class TopUpCampaignRecipientConfiguration : IEntityTypeConfiguration<TopUpCampaignRecipient>
{
    public void Configure(EntityTypeBuilder<TopUpCampaignRecipient> builder)
    {
        builder.ToTable("TopUpCampaignRecipient", "topup");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("TopUpCampaignRecipientId").UseIdentityColumn();
        builder.HasIndex(x => new { x.TopUpCampaignId, x.EducationAccountId }).IsUnique();
        builder.Property(x => x.AmountOverride).HasPrecision(19, 2);
        builder.Property(x => x.AddedAtUtc).HasColumnName("AddedAt");
    }
}
