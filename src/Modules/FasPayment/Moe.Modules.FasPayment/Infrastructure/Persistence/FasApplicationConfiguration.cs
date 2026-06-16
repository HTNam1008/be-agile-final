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
        builder.Property(x => x.FasSchemeId).HasColumnName("FASSchemeId");
        builder.HasIndex(x => x.ApplicationNumber).IsUnique();
        builder.HasIndex(x => new { x.PersonId, x.FasSchemeId, x.CourseId });
        builder.Property(x => x.ApplicationNumber).HasMaxLength(50).IsRequired();
        builder.Property(x => x.ApplicationStatusCode).HasMaxLength(30).IsUnicode(false).IsRequired();
        builder.Property(x => x.NationalitySnapshot).HasMaxLength(50);
        builder.Property(x => x.HouseholdIncomeSnapshot).HasPrecision(19, 2);
        builder.Property(x => x.PerCapitaIncomeSnapshot).HasPrecision(19, 2);
        builder.Property(x => x.EvaluationResultCode).HasMaxLength(50).IsUnicode(false);
        builder.Property(x => x.EvaluatedAtUtc).HasColumnName("EvaluatedAt");
        builder.Property(x => x.ApplicantConfirmedAtUtc).HasColumnName("ApplicantConfirmedAt");
        builder.Property(x => x.SubmittedAtUtc).HasColumnName("SubmittedAt");
    }
}
