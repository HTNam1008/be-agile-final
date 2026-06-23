using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Moe.Modules.EducationAccountTopUp.Domain.TopUps;

namespace Moe.Modules.EducationAccountTopUp.Infrastructure.Persistence;

internal sealed class TopUpCampaignConfiguration : IEntityTypeConfiguration<TopUpCampaign>
{
    public void Configure(EntityTypeBuilder<TopUpCampaign> builder)
    {
        builder.ToTable("TopUpCampaign", "topup", t =>
        {
            t.HasCheckConstraint("CK_TopUpCampaign_Amount", "[DefaultTopUpAmount] > 0");
            t.HasCheckConstraint("CK_TopUpCampaign_Schedule", "[ScheduleTypeCode] != 'RECURRING' OR ([FrequencyCode] IS NOT NULL AND [FrequencyInterval] IS NOT NULL AND [EndDate] IS NOT NULL AND [EndDate] >= [StartDate])");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("TopUpCampaignId").UseIdentityColumn();
        builder.HasIndex(x => new { x.OrganizationId, x.CampaignCode }).IsUnique();
        builder.Property(x => x.CampaignCode).HasMaxLength(50).IsRequired();
        builder.Property(x => x.CampaignName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(1000);
        builder.Property(x => x.RecipientModeCode).HasMaxLength(30).IsUnicode(false).IsRequired();
        builder.Property(x => x.DefaultTopUpAmount).HasPrecision(19, 2);
        builder.Property(x => x.Reason).HasMaxLength(1000).IsRequired();
        builder.Property(x => x.ScheduleTypeCode).HasMaxLength(30).IsUnicode(false).IsRequired();
        builder.Property(x => x.FrequencyCode).HasMaxLength(30).IsUnicode(false);
        builder.Property(x => x.NextRunAtUtc).HasColumnName("NextRunAt");
        builder.Property(x => x.CampaignStatusCode).HasMaxLength(30).IsUnicode(false).IsRequired();
        builder.Property(x => x.CampaignVersion).IsConcurrencyToken();
        builder.Property(x => x.CreatedAtUtc).HasColumnName("CreatedAt");
        builder.Property(x => x.UpdatedAtUtc).HasColumnName("UpdatedAt");
    }
}
