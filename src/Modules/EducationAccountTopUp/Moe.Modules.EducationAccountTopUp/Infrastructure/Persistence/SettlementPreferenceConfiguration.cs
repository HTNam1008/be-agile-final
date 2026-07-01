using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Moe.Modules.EducationAccountTopUp.Domain.EducationAccounts;

namespace Moe.Modules.EducationAccountTopUp.Infrastructure.Persistence;

internal sealed class SettlementPreferenceConfiguration : IEntityTypeConfiguration<SettlementPreference>
{
    public void Configure(EntityTypeBuilder<SettlementPreference> builder)
    {
        builder.ToTable("SettlementPreference", "account");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("SettlementPreferenceId").UseIdentityColumn();
        builder.HasIndex(x => x.EducationAccountId)
            .IsUnique()
            .HasFilter("[IsActive] = 1");
        builder.HasOne<EducationAccount>()
            .WithMany()
            .HasForeignKey(x => x.EducationAccountId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.DestinationTypeCode).HasMaxLength(30).IsUnicode(false).IsRequired();
        builder.Property(x => x.DestinationToken).HasMaxLength(300).IsRequired();
        builder.Property(x => x.DestinationMasked).HasMaxLength(100).IsRequired();
        builder.Property(x => x.UpdatedAtUtc).HasColumnName("UpdatedAt");
    }
}
