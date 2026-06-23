using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Moe.Modules.EducationAccountTopUp.Domain.EducationAccounts;

namespace Moe.Modules.EducationAccountTopUp.Infrastructure.Persistence;

internal sealed class AccountSettlementConfiguration : IEntityTypeConfiguration<AccountSettlement>
{
    public void Configure(EntityTypeBuilder<AccountSettlement> builder)
    {
        builder.ToTable("AccountSettlement", "account");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("AccountSettlementId").UseIdentityColumn();
        builder.HasIndex(x => x.EducationAccountId);
        builder.Property(x => x.SettlementAmount).HasPrecision(19, 2);
        builder.Property(x => x.DestinationTypeCode).HasMaxLength(30).IsUnicode(false).IsRequired();
        builder.Property(x => x.DestinationToken).HasMaxLength(300).IsRequired();
        builder.Property(x => x.DestinationMasked).HasMaxLength(100).IsRequired();
        builder.Property(x => x.SettlementStatusCode).HasMaxLength(30).IsUnicode(false).IsRequired();
        builder.Property(x => x.ProviderReference).HasMaxLength(150);
        builder.Property(x => x.RequestedAtUtc).HasColumnName("RequestedAt");
        builder.Property(x => x.CompletedAtUtc).HasColumnName("CompletedAt");
        builder.Property(x => x.FailureReason).HasMaxLength(1000);
    }
}
