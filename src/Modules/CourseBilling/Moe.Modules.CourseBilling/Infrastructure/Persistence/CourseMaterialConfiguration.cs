using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Moe.Modules.CourseBilling.Domain.Courses;

namespace Moe.Modules.CourseBilling.Infrastructure.Persistence;

internal sealed class CourseMaterialConfiguration : IEntityTypeConfiguration<CourseMaterial>
{
    public void Configure(EntityTypeBuilder<CourseMaterial> builder)
    {
        builder.ToTable("CourseMaterial", "course");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("CourseMaterialId").UseIdentityColumn();
        builder.HasIndex(x => x.CourseId);
        builder.HasIndex(x => new { x.CourseId, x.IsActive });
        builder.HasIndex(x => new { x.CourseId, x.StoragePath }).IsUnique();
        builder.Property(x => x.MaterialTitle).HasMaxLength(200).IsRequired();
        builder.Property(x => x.MaterialDescription).HasMaxLength(1000);
        builder.Property(x => x.MaterialTypeCode).HasMaxLength(40).IsUnicode(false).IsRequired();
        builder.Property(x => x.FileName).HasMaxLength(260).IsRequired();
        builder.Property(x => x.OriginalFileName).HasMaxLength(260).IsRequired();
        builder.Property(x => x.FileExtension).HasMaxLength(20).IsUnicode(false).IsRequired();
        builder.Property(x => x.ContentType).HasMaxLength(100).IsRequired();
        builder.Property(x => x.StorageProviderCode).HasMaxLength(40).IsUnicode(false).IsRequired();
        builder.Property(x => x.StoragePath).HasMaxLength(600).IsRequired();
        builder.Property(x => x.PublicUrl).HasMaxLength(1000);
        builder.Property(x => x.UploadedAtUtc).HasColumnName("UploadedAt");
        builder.Property(x => x.UpdatedAtUtc).HasColumnName("UpdatedAt");
        builder.Property(x => x.DeletedAtUtc).HasColumnName("DeletedAt");
    }
}
