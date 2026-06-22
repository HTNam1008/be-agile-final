using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Moe.Modules.FasPayment.Domain.Fas;

namespace Moe.Modules.FasPayment.Infrastructure.Persistence;

internal sealed class FasApplicationConfiguration : IEntityTypeConfiguration<FasApplication>
{
    public void Configure(EntityTypeBuilder<FasApplication> builder)
    {
        builder.ToTable("FASApplication", "fas");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("FASApplicationId").UseIdentityColumn();

        builder.Property(x => x.ApplicationNo).HasColumnName("application_no").HasMaxLength(50).IsRequired();
        builder.HasIndex(x => x.ApplicationNo).IsUnique();

        builder.Property(x => x.FasSchemeId).HasColumnName("scheme_id");

        builder.Property(x => x.StudentId).HasColumnName("student_id").HasMaxLength(50).IsRequired();
        builder.Property(x => x.StudentName).HasColumnName("student_name").HasMaxLength(255).IsRequired();

        builder.Property(x => x.SubmittedDate).HasColumnName("submitted_date").HasColumnType("DATE").IsRequired();

        builder.Property(x => x.StatusCode).HasColumnName("status").HasMaxLength(30).IsRequired();
        builder.ToTable(t => t.HasCheckConstraint("chk_fas_application_status", "status IN ('PENDING_REVIEW', 'APPROVED', 'REJECTED')"));

        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("TIMESTAMP").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasColumnType("TIMESTAMP");
    }
}
