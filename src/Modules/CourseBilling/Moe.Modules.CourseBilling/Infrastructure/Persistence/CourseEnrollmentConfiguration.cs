using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Moe.Modules.CourseBilling.Domain.Courses;

namespace Moe.Modules.CourseBilling.Infrastructure.Persistence;

internal sealed class CourseEnrollmentConfiguration : IEntityTypeConfiguration<CourseEnrollment>
{
    public void Configure(EntityTypeBuilder<CourseEnrollment> builder)
    {
        builder.ToTable("CourseEnrollment", "course");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("CourseEnrollmentId").UseIdentityColumn();
        builder.HasIndex(x => new { x.PersonId, x.CourseId })
            .IsUnique()
            .HasFilter("[EnrollmentStatusCode] NOT IN ('CANCELLED', 'REFUNDED', 'EXITED')");
        builder.HasIndex(x => x.CoursePaymentPlanId);
        builder.Property(x => x.EnrollmentSourceCode).HasMaxLength(30).IsUnicode(false).IsRequired();
        builder.Property(x => x.EnrolledAtUtc).HasColumnName("EnrolledAt");
        builder.Property(x => x.EnrollmentStatusCode).HasMaxLength(30).IsUnicode(false).IsRequired();
        builder.Property(x => x.BeforeStartRefundPercentage).HasPrecision(5, 2).IsRequired();
        builder.Property(x => x.AfterStartRefundPercentage).HasPrecision(5, 2).IsRequired();
        builder.Property(x => x.ExitAtUtc).HasColumnName("ExitAt");
        builder.Property(x => x.ExitReasonCode).HasMaxLength(50).IsUnicode(false);
        builder.Property(x => x.RowVersion).IsRowVersion();
    }
}
