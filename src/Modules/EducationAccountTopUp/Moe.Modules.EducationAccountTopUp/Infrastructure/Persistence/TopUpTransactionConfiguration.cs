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
        builder.HasIndex(x => x.IdempotencyKey)
            .IsUnique()
            .HasDatabaseName("IX_TopUpTransaction_IdempotencyKey");

        builder.HasIndex(x => new { x.TopUpRunId, x.EducationAccountId })
            .IsUnique()
            .HasDatabaseName("IX_TopUpTransaction_Run_Account");

        builder.Property(x => x.IdempotencyKey)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.TransactionStatusCode)
            .HasMaxLength(30)
            .IsUnicode(false)
            .IsRequired();

        builder.Property(x => x.Amount)
            .HasColumnType("decimal(18,2)")
            .IsRequired();

        builder.Property(x => x.Reason)
            .HasMaxLength(1000)
            .IsRequired(false);

        builder.Property(x => x.CreatedAtUtc)
            .HasColumnName("CreatedAt")
            .IsRequired();

        builder.Property(x => x.CompletedAtUtc)
            .HasColumnName("CompletedAt")
            .IsRequired(false);

        builder.HasOne<TopUpRun>()
            .WithMany()
            .HasForeignKey(x => x.TopUpRunId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
