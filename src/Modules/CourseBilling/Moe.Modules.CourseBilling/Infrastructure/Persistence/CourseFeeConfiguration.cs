using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Moe.Modules.CourseBilling.Domain.Courses;

namespace Moe.Modules.CourseBilling.Infrastructure.Persistence;

internal sealed class CourseFeeConfiguration : IEntityTypeConfiguration<CourseFee>
{
    public void Configure(EntityTypeBuilder<CourseFee> builder)
    {
        builder.ToTable("CourseFee", "course");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("CourseFeeId").UseIdentityColumn();
        builder.HasIndex(x => new { x.CourseId, x.FeeComponentId }).IsUnique();
        builder.Property(x => x.FeeValue).HasPrecision(19, 4);
    }
}
