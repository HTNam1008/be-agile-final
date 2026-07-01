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
        builder.Ignore(x => x.ScheduledFor);
        builder.Ignore(x => x.StartedAt);
        builder.Ignore(x => x.CompletedAt);
        builder.Property(x => x.Id).HasColumnName("TopUpRunId").UseIdentityColumn();
        builder.HasIndex(x => x.TopUpCampaignId);
        builder.HasIndex(x => x.IdempotencyKey)
            .IsUnique()
            .HasDatabaseName("IX_TopUpRun_IdempotencyKey");

        builder.HasIndex(x => new { x.TopUpCampaignId, x.ScheduledForUtc })
            .IsUnique()
            .HasFilter("[ScheduledFor] IS NOT NULL")
            .HasDatabaseName("IX_TopUpRuns_Campaign_ScheduledFor");
        builder.HasIndex(x => x.CancelRequestedAtUtc)
            .HasFilter("[CancelRequestedAt] IS NOT NULL")
            .HasDatabaseName("IX_TopUpRun_CancelRequested");
        builder.Property(x => x.ScheduledForUtc).HasColumnName("ScheduledFor");
        builder.Property(x => x.TriggerTypeCode).HasMaxLength(30).IsUnicode(false).IsRequired();
        builder.Property(x => x.RunStatusCode).HasMaxLength(30).IsUnicode(false).IsRequired();
        builder.Property(x => x.RuleSnapshotJson);
        builder.Property(x => x.IsContractDriven).HasDefaultValue(false);
        builder.Property(x => x.RunTypeCode).HasMaxLength(20).IsUnicode(false);
        builder.Property(x => x.CreatedAtUtc).HasColumnName("CreatedAt");
        builder.Property(x => x.TotalSelected).HasDefaultValue(0);
        builder.Property(x => x.TotalProcessed).HasDefaultValue(0);
        builder.Property(x => x.TotalSucceeded).HasDefaultValue(0);
        builder.Property(x => x.TotalFailed).HasDefaultValue(0);
        builder.Property(x => x.TotalSkipped).HasDefaultValue(0);
        builder.Property(x => x.TotalAmount).HasColumnType("decimal(18,2)").HasDefaultValue(0m);
        builder.Property(x => x.StartedAtUtc).HasColumnName("StartedAt");
        builder.Property(x => x.CompletedAtUtc).HasColumnName("CompletedAt");
        builder.Property(x => x.CancelRequestedAtUtc).HasColumnName("CancelRequestedAt");
        builder.Property(x => x.IdempotencyKey).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Note).HasMaxLength(500);
        builder.HasOne<TopUpCampaign>()
            .WithMany()
            .HasForeignKey(x => x.TopUpCampaignId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
