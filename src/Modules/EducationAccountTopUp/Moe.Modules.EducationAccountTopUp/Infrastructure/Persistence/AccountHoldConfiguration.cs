using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Moe.Modules.EducationAccountTopUp.Domain.EducationAccounts;

namespace Moe.Modules.EducationAccountTopUp.Infrastructure.Persistence;

internal sealed class AccountHoldConfiguration : IEntityTypeConfiguration<AccountHold>
{
    public void Configure(EntityTypeBuilder<AccountHold> builder)
    {
        builder.ToTable("AccountHold", "account");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("AccountHoldId").UseIdentityColumn();
        builder.HasIndex(x => x.EducationAccountId);
        builder.HasIndex(x => x.PaymentPartId).IsUnique().HasFilter("[PaymentPartId] IS NOT NULL");
        builder.Property(x => x.HoldAmount).HasPrecision(19, 2);
        builder.Property(x => x.HoldStatusCode).HasMaxLength(30).IsUnicode(false).IsRequired();
        builder.Property(x => x.CreatedAtUtc).HasColumnName("CreatedAt");
        builder.Property(x => x.ExpiresAtUtc).HasColumnName("ExpiresAt");
        builder.Property(x => x.ConvertedAtUtc).HasColumnName("ConvertedAt");
    }
}
