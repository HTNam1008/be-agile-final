using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Moe.Modules.FasPayment.Domain.Fas;

namespace Moe.Modules.FasPayment.Infrastructure.Persistence;

internal sealed class FasSchemeConfiguration : IEntityTypeConfiguration<FasScheme>
{
    public void Configure(EntityTypeBuilder<FasScheme> builder)
    {
        builder.ToTable("FASScheme", "fas");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("FASSchemeId").UseIdentityColumn();
        builder.HasIndex(x => x.SchemeCode).IsUnique();
        builder.Property(x => x.SchemeCode).HasMaxLength(50).IsRequired();
        builder.Property(x => x.SchemeName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(1000);
        builder.Property(x => x.ProviderName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.SchemeStatusCode).HasMaxLength(30).IsUnicode(false).IsRequired();
    }
}
