using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Moe.Modules.EducationAccountTopUp.Domain.TopUps;

namespace Moe.Modules.EducationAccountTopUp.Infrastructure.Persistence;

internal sealed class TopUpCampaignRuleConfiguration : IEntityTypeConfiguration<TopUpCampaignRule>
{
    public void Configure(EntityTypeBuilder<TopUpCampaignRule> builder)
    {
        builder.ToTable("TopUpCampaignRule", "topup");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("TopUpCampaignRuleId").UseIdentityColumn();
        builder.HasIndex(x => x.TopUpCampaignId);
        builder.Property(x => x.CriterionCode).HasMaxLength(50).IsUnicode(false).IsRequired();
        builder.Property(x => x.OperatorCode).HasMaxLength(30).IsUnicode(false).IsRequired();
        builder.Property(x => x.NumericValueFrom).HasPrecision(19, 2);
        builder.Property(x => x.NumericValueTo).HasPrecision(19, 2);
        builder.Property(x => x.TextValue).HasMaxLength(300);
    }
}
