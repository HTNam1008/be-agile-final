using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Moe.Modules.FasPayment.Domain.Fas;

namespace Moe.Modules.FasPayment.Infrastructure.Persistence;

internal sealed class FasSchemeConfiguration : IEntityTypeConfiguration<FasScheme>
{
    public void Configure(EntityTypeBuilder<FasScheme> builder)
    {
        builder.ToTable("FASScheme", "fas", table =>
        {
            table.HasCheckConstraint("CK_FASScheme_Status", "[StatusCode] IN ('DRAFT','ACTIVE','RETIRED','DISABLED','DELETED')");
            table.HasCheckConstraint("CK_FASScheme_Dates", "[EndDate] >= [StartDate]");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("FASSchemeId").UseIdentityColumn();
        builder.Property(x => x.SchemeCode).HasMaxLength(50).IsUnicode(false).IsRequired();
        builder.Property(x => x.GrantCode).HasMaxLength(100).IsUnicode(false).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(255).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(2000);
        builder.Property(x => x.StatusCode).HasMaxLength(30).IsUnicode(false).IsRequired();
        builder.Property(x => x.CreatedAtUtc).HasColumnName("CreatedAt");
        builder.Property(x => x.UpdatedAtUtc).HasColumnName("UpdatedAt");
        builder.Property(x => x.ActivatedAtUtc).HasColumnName("ActivatedAt");
        builder.HasIndex(x => x.SchemeCode).IsUnique();
        builder.HasIndex(x => x.GrantCode).IsUnique();
    }
}
