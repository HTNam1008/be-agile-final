using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Moe.Modules.EducationAccountTopUp.Domain.TopUps;

namespace Moe.Modules.EducationAccountTopUp.Infrastructure.Persistence;

internal sealed class TopUpTransactionConfiguration : IEntityTypeConfiguration<TopUpTransaction>
{
    public void Configure(EntityTypeBuilder<TopUpTransaction> builder)
    {
        builder.ToTable("TopUpTransaction", "topup");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("TopUpTransactionId").UseIdentityColumn();
        builder.HasIndex(x => new { x.TopUpRunId, x.EducationAccountId }).IsUnique();
        builder.HasIndex(x => x.IdempotencyKey).IsUnique();
        builder.Property(x => x.TopUpAmount).HasPrecision(19, 2);
        builder.Property(x => x.Reason).HasMaxLength(1000).IsRequired();
        builder.Property(x => x.TransactionStatusCode).HasMaxLength(30).IsUnicode(false).IsRequired();
        builder.Property(x => x.ProcessedAtUtc).HasColumnName("ProcessedAt");
        builder.Property(x => x.FailureReason).HasMaxLength(1000);
        builder.Property(x => x.IdempotencyKey).HasMaxLength(120).IsRequired();
    }
}
