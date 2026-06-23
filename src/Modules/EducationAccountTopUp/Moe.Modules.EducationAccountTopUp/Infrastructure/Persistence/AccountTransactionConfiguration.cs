using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Moe.Modules.EducationAccountTopUp.Domain.EducationAccounts;

namespace Moe.Modules.EducationAccountTopUp.Infrastructure.Persistence;

internal sealed class AccountTransactionConfiguration : IEntityTypeConfiguration<AccountTransaction>
{
    public void Configure(EntityTypeBuilder<AccountTransaction> builder)
    {
        builder.ToTable("AccountTransaction", "account");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("AccountTransactionId").UseIdentityColumn();
        builder.HasIndex(x => new { x.EducationAccountId, x.TransactionAtUtc });
        builder.HasIndex(x => x.IdempotencyKey).IsUnique();
        builder.Property(x => x.TransactionTypeCode).HasMaxLength(50).IsUnicode(false).IsRequired();
        builder.Property(x => x.Amount).HasPrecision(19, 2);
        builder.Property(x => x.TransactionAtUtc).HasColumnName("TransactionAt");
        builder.Property(x => x.ReferenceTypeCode).HasMaxLength(50).IsUnicode(false).IsRequired();
        builder.Property(x => x.IdempotencyKey).HasMaxLength(120).IsRequired();
        builder.Property(x => x.BalanceAfter).HasPrecision(19, 2);
        builder.HasIndex(x => x.ReversalOfTransactionId);
        builder.Property(x => x.Description).HasMaxLength(1000);
    }
}
