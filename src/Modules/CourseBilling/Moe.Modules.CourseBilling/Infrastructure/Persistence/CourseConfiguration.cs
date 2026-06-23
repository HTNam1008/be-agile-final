using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Moe.Modules.CourseBilling.Domain.Courses;

namespace Moe.Modules.CourseBilling.Infrastructure.Persistence;

internal sealed class CourseConfiguration : IEntityTypeConfiguration<Course>
{
    public void Configure(EntityTypeBuilder<Course> builder)
    {
        builder.ToTable("Course", "course");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("CourseId").UseIdentityColumn();
        builder.Property(x => x.CourseCode).HasMaxLength(50).IsRequired();
        builder.Property(x => x.CourseName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(1000);
        //  builder.Property(x => x.AcademicYear).HasMaxLength(20).IsUnicode(false).IsRequired();
        builder.Property(x => x.EnrollmentOpenAtUtc).HasColumnName("EnrollmentOpenAt");
        builder.Property(x => x.EnrollmentCloseAtUtc).HasColumnName("EnrollmentCloseAt");
        builder.Property(x => x.BeforeStartRefundPercentage).HasPrecision(5, 2).IsRequired();
        builder.Property(x => x.AfterStartRefundPercentage).HasPrecision(5, 2).IsRequired();
        builder.Property(x => x.CourseStatusCode).HasMaxLength(30).IsUnicode(false).IsRequired();
        //  builder.Property(x => x.CreatedAtUtc).HasColumnName("CreatedAt");
        builder.Property(x => x.UpdatedAtUtc).HasColumnName("UpdatedAt");
        builder.Property(x => x.DisabledAtUtc).HasColumnName("DisabledAt");
        //  builder.Property(x => x.DisabledReason).HasMaxLength(1000);
    }
}
