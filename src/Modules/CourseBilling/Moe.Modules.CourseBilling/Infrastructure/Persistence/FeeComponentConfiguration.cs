using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Moe.Modules.CourseBilling.Domain.Courses;

namespace Moe.Modules.CourseBilling.Infrastructure.Persistence;

internal sealed class FeeComponentConfiguration : IEntityTypeConfiguration<FeeComponent>
{
    public void Configure(EntityTypeBuilder<FeeComponent> builder)
    {
        builder.ToTable("FeeComponent", "course");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("FeeComponentId").UseIdentityColumn();
        builder.HasIndex(x => x.ComponentCode).IsUnique();
        builder.Property(x => x.ComponentCode).HasMaxLength(50).IsRequired();
        builder.Property(x => x.ComponentName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.ComponentTypeCode).HasMaxLength(30).IsUnicode(false).IsRequired();
        builder.Property(x => x.CalculationTypeCode).HasMaxLength(30).IsUnicode(false).IsRequired();
        builder.Property(x => x.DefaultValue).HasPrecision(19, 4);
    }
}
