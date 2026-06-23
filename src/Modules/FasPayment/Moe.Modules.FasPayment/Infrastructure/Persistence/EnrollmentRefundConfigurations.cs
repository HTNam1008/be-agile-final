using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Moe.Modules.FasPayment.Domain.Payments;

namespace Moe.Modules.FasPayment.Infrastructure.Persistence;

internal sealed class EnrollmentRefundConfiguration : IEntityTypeConfiguration<EnrollmentRefund>
{
    public void Configure(EntityTypeBuilder<EnrollmentRefund> builder)
    {
        builder.ToTable("EnrollmentRefund", "payment");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("EnrollmentRefundId").UseIdentityColumn();
        builder.HasIndex(x => x.IdempotencyKey).IsUnique();
        builder.HasIndex(x => x.CourseEnrollmentId);
        builder.Property(x => x.PaidAmount).HasPrecision(19, 2);
        builder.Property(x => x.RefundPercentage).HasPrecision(5, 2);
        builder.Property(x => x.RefundAmount).HasPrecision(19, 2);
        builder.Property(x => x.EducationAccountRefundAmount).HasPrecision(19, 2);
        builder.Property(x => x.OnlineRefundAmount).HasPrecision(19, 2);
        builder.Property(x => x.PolicyPeriodCode).HasMaxLength(30).IsUnicode(false).IsRequired();
        builder.Property(x => x.RefundStatusCode).HasMaxLength(30).IsUnicode(false).IsRequired();
        builder.Property(x => x.IdempotencyKey).HasMaxLength(120).IsRequired();
        builder.Property(x => x.RequestedAtUtc).HasColumnName("RequestedAt");
        builder.Property(x => x.CompletedAtUtc).HasColumnName("CompletedAt");
        builder.Property(x => x.FailureReason).HasMaxLength(1000);
    }
}

internal sealed class EnrollmentRefundPartConfiguration : IEntityTypeConfiguration<EnrollmentRefundPart>
{
    public void Configure(EntityTypeBuilder<EnrollmentRefundPart> builder)
    {
        builder.ToTable("EnrollmentRefundPart", "payment");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("EnrollmentRefundPartId").UseIdentityColumn();
        builder.HasIndex(x => x.IdempotencyKey).IsUnique();
        builder.HasIndex(x => x.EnrollmentRefundId);
        builder.HasIndex(x => x.ProviderRefundId).IsUnique().HasFilter("[ProviderRefundId] IS NOT NULL");
        builder.Property(x => x.RefundMethodCode).HasMaxLength(30).IsUnicode(false).IsRequired();
        builder.Property(x => x.RefundAmount).HasPrecision(19, 2);
        builder.Property(x => x.RefundStatusCode).HasMaxLength(30).IsUnicode(false).IsRequired();
        builder.Property(x => x.ProviderRefundId).HasMaxLength(100);
        builder.Property(x => x.IdempotencyKey).HasMaxLength(120).IsRequired();
        builder.Property(x => x.CreatedAtUtc).HasColumnName("CreatedAt");
        builder.Property(x => x.CompletedAtUtc).HasColumnName("CompletedAt");
        builder.Property(x => x.FailureReason).HasMaxLength(1000);
    }
}
