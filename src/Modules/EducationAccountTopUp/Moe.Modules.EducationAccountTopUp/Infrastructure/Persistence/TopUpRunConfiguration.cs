using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Moe.Modules.EducationAccountTopUp.Domain.TopUps;

namespace Moe.Modules.EducationAccountTopUp.Infrastructure.Persistence;

internal sealed class TopUpRunConfiguration : IEntityTypeConfiguration<TopUpRun>
{
    public void Configure(EntityTypeBuilder<TopUpRun> builder)
    {
        builder.ToTable("TopUpRun", "topup");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("TopUpRunId").UseIdentityColumn();
        builder.HasIndex(x => x.TopUpCampaignId);
        builder.HasIndex(x => x.IdempotencyKey).IsUnique();
        builder.Property(x => x.ScheduledForUtc).HasColumnName("ScheduledFor");
        builder.Property(x => x.TriggerTypeCode).HasMaxLength(30).IsUnicode(false).IsRequired();
        builder.Property(x => x.RunStatusCode).HasMaxLength(30).IsUnicode(false).IsRequired();
        builder.Property(x => x.RuleSnapshotJson).HasColumnType("nvarchar(max)");
        builder.Property(x => x.TotalAmount).HasPrecision(19, 2);
        builder.Property(x => x.StartedAtUtc).HasColumnName("StartedAt");
        builder.Property(x => x.CompletedAtUtc).HasColumnName("CompletedAt");
        builder.Property(x => x.IdempotencyKey).HasMaxLength(120).IsRequired();
    }
}
