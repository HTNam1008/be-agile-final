using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Moe.Modules.FasPayment.Domain.Fas;

namespace Moe.Modules.FasPayment.Infrastructure.Persistence;

internal sealed class CourseFasSchemeConfiguration : IEntityTypeConfiguration<CourseFasScheme>
{
    public void Configure(EntityTypeBuilder<CourseFasScheme> builder)
    {
        builder.ToTable("CourseFASScheme", "fas");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("CourseFASSchemeId").UseIdentityColumn();
        builder.HasIndex(x => new { x.CourseId, x.FasSchemeId }).IsUnique();
        builder.Property(x => x.FasSchemeId).HasColumnName("FASSchemeId");
        builder.Property(x => x.StatusCode).HasMaxLength(30).IsUnicode(false).IsRequired();
    }
}
