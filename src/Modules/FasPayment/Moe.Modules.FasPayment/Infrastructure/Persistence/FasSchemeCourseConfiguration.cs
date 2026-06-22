using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Moe.Modules.FasPayment.Domain.Fas;

namespace Moe.Modules.FasPayment.Infrastructure.Persistence;

internal sealed class FasSchemeCourseConfiguration : IEntityTypeConfiguration<FasSchemeCourse>
{
    public void Configure(EntityTypeBuilder<FasSchemeCourse> builder)
    {
        builder.ToTable("FASSchemeCourse", "fas");
        builder.HasKey(x => new { x.FasSchemeId, x.CourseId });
        builder.Property(x => x.FasSchemeId).HasColumnName("FASSchemeId");
        builder.Property(x => x.CourseId).HasColumnName("CourseId");
        builder.Property(x => x.CreatedAtUtc).HasColumnName("CreatedAt");
        builder.HasOne<FasScheme>().WithMany().HasForeignKey(x => x.FasSchemeId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne("Moe.Modules.CourseBilling.Domain.Courses.Course", null).WithMany().HasForeignKey(nameof(FasSchemeCourse.CourseId)).OnDelete(DeleteBehavior.Restrict);
    }
}
