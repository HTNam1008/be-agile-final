using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Moe.Modules.CourseBilling.Domain.Billing;

namespace Moe.Modules.CourseBilling.Infrastructure.Persistence;

internal sealed class BillConfiguration : IEntityTypeConfiguration<Bill>
{
    public void Configure(EntityTypeBuilder<Bill> builder)
    {
        builder.ToTable("Bill", "billing");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("BillId").UseIdentityColumn();
        builder.HasIndex(x => x.BillNumber).IsUnique();
        builder.HasIndex(x => x.CourseEnrollmentId);
        builder.Property(x => x.BillNumber).HasMaxLength(50).IsRequired();
        builder.Property(x => x.IssuedAtUtc).HasColumnName("IssuedAt");
        builder.Property(x => x.GrossAmount).HasPrecision(19, 2);
        builder.Property(x => x.SubsidyAmount).HasPrecision(19, 2);
        builder.Property(x => x.NetPayableAmount).HasPrecision(19, 2);
        builder.Property(x => x.PaidAmount).HasPrecision(19, 2);
        builder.Property(x => x.OutstandingAmount).HasPrecision(19, 2);
        builder.Property(x => x.BillStatusCode).HasMaxLength(30).IsUnicode(false).IsRequired();
    }
}
