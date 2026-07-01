using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Moe.Modules.EducationAccountTopUp.Domain.TopUps;

namespace Moe.Modules.EducationAccountTopUp.Infrastructure.Persistence;

internal sealed class DynamicTopUpContractConfiguration : IEntityTypeConfiguration<DynamicTopUpContract>
{
    public void Configure(EntityTypeBuilder<DynamicTopUpContract> builder)
    {
        builder.ToTable("DynamicTopUpContract", "topup");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("DynamicTopUpContractId").UseIdentityColumn();
        builder.HasIndex(x => new { x.TopUpCampaignId, x.EducationAccountId }).IsUnique();
        builder.Property(x => x.DeliveryTypeCode).HasMaxLength(30).IsUnicode(false).IsRequired();
        builder.Property(x => x.AmountPerPayment).HasPrecision(19, 2).IsRequired();
        builder.Property(x => x.MaxTotalAmount).HasPrecision(19, 2).IsRequired();
        builder.Property(x => x.TotalReceived).HasPrecision(19, 2).IsRequired();
        builder.Property(x => x.FrequencyCode).HasMaxLength(30).IsUnicode(false).IsRequired();
        builder.Property(x => x.ContractStatus).HasMaxLength(30).IsUnicode(false).IsRequired();
        builder.Property(x => x.CreatedAtUtc).HasColumnName("CreatedAt");
        builder.Property(x => x.UpdatedAtUtc).HasColumnName("UpdatedAt");
    }
}
