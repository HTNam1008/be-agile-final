using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Moe.Modules.CourseBilling.Domain.Courses;

namespace Moe.Modules.CourseBilling.Infrastructure.Persistence;

internal sealed class CourseTargetConfiguration : IEntityTypeConfiguration<CourseTarget>
{
    public void Configure(EntityTypeBuilder<CourseTarget> builder)
    {
        builder.ToTable("CourseTarget", "course");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("CourseTargetId").UseIdentityColumn();
        builder.HasIndex(x => x.CourseId);
        builder.Property(x => x.TargetTypeCode).HasMaxLength(30).IsUnicode(false).IsRequired();
        builder.Property(x => x.LevelCode).HasMaxLength(30).IsUnicode(false);
        builder.Property(x => x.ClassCode).HasMaxLength(30).IsUnicode(false);
    }
}
