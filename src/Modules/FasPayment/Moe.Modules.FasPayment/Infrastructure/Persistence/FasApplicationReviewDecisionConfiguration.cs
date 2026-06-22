using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Moe.Modules.FasPayment.Domain.Fas;

namespace Moe.Modules.FasPayment.Infrastructure.Persistence;

internal sealed class FasApplicationReviewDecisionConfiguration : IEntityTypeConfiguration<FasApplicationReviewDecision>
{
    public void Configure(EntityTypeBuilder<FasApplicationReviewDecision> builder)
    {
        builder.ToTable("FASApplicationReviewDecision", "fas", table =>
        {
            table.HasCheckConstraint("CK_FASReviewDecision_Decision", "[Decision] IN ('APPROVED','REJECTED')");
            table.HasCheckConstraint("CK_FASReviewDecision_RejectionReason", "[Decision] <> 'REJECTED' OR [RejectionReasonCode] IS NOT NULL");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("FASApplicationReviewDecisionId").UseIdentityColumn();
        builder.Property(x => x.FasApplicationId).HasColumnName("FASApplicationId");
        builder.Property(x => x.Decision).HasMaxLength(30).IsUnicode(false).IsRequired();
        builder.Property(x => x.ReviewedAtUtc).HasColumnName("ReviewedAt");
        builder.Property(x => x.RejectionReasonCode).HasMaxLength(100).IsUnicode(false);
        builder.Property(x => x.Remarks).HasMaxLength(2000);
        builder.HasOne<FasApplication>().WithMany().HasForeignKey(x => x.FasApplicationId).OnDelete(DeleteBehavior.Restrict);
    }
}
