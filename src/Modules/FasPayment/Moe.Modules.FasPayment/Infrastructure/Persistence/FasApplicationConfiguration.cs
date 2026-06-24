using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Moe.Modules.FasPayment.Domain.Fas;

namespace Moe.Modules.FasPayment.Infrastructure.Persistence;

internal sealed class FasApplicationConfiguration : IEntityTypeConfiguration<FasApplication>
{
    public void Configure(EntityTypeBuilder<FasApplication> b)
    {
        b.ToTable("FASApplication", "fas", t => { t.HasCheckConstraint("CK_FASApplication_Status", "[ApplicationStatusCode] IN ('DRAFT','SUBMITTED','WITHDRAWN','PENDING_REVIEW','APPROVED','REJECTED')"); t.HasCheckConstraint("CK_FASApplication_HouseholdSize", "[HouseholdSizeSnapshot] IS NULL OR [HouseholdSizeSnapshot] > 0"); t.HasCheckConstraint("CK_FASApplication_Income", "[HouseholdIncomeSnapshot] IS NULL OR [HouseholdIncomeSnapshot] >= 0"); t.HasCheckConstraint("CK_FASApplication_PCI", "[PerCapitaIncomeSnapshot] IS NULL OR [PerCapitaIncomeSnapshot] >= 0"); }); b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("FASApplicationId").UseIdentityColumn();
        b.Property(x => x.ApplicationNo).HasColumnName("ApplicationNumber").HasMaxLength(50).IsRequired(); b.HasIndex(x => x.ApplicationNo).IsUnique();
        b.Property(x => x.FasSchemeId).HasColumnName("FASSchemeId");
        b.Property(x => x.AccountHolderPersonId).HasColumnName("AccountHolderPersonId");
        b.Property(x => x.StudentPersonId).HasColumnName("PersonId");
        b.Property(x => x.StudentId).HasColumnName("StudentNumberSnapshot").HasMaxLength(50);
        b.Property(x => x.StudentName).HasColumnName("StudentNameSnapshot").HasMaxLength(255);
        b.Property(x => x.NricFinMasked).HasMaxLength(30); b.Property(x => x.NationalityCode).HasColumnName("NationalitySnapshot").HasMaxLength(50);
        b.Property(x => x.ParentNationalitiesJson).HasColumnName("ParentNationalitiesJson").HasMaxLength(2000);
        b.Property(x => x.AccountTypeCode).HasColumnName("AccountTypeCode").HasMaxLength(30).IsUnicode(false).IsRequired();
        b.Property(x => x.Mobile).HasMaxLength(50); b.Property(x => x.Address).HasMaxLength(1000); b.Property(x => x.Email).HasMaxLength(320);
        b.Property(x => x.SchoolName).HasMaxLength(255); b.Property(x => x.StudentNumber).HasMaxLength(50); b.Property(x => x.EmploymentStatusCode).HasMaxLength(30).IsUnicode(false);
        b.Property(x => x.MonthlyHouseholdIncome).HasColumnName("HouseholdIncomeSnapshot").HasPrecision(19, 2);
        b.Property(x => x.HouseholdMemberCount).HasColumnName("HouseholdSizeSnapshot"); b.Property(x => x.OtherMonthlyIncome).HasPrecision(19, 2);
        b.Property(x => x.PerCapitaIncome).HasColumnName("PerCapitaIncomeSnapshot").HasPrecision(19, 2);
        b.Property(x => x.SubmittedDate).HasColumnName("SubmittedDateSnapshot"); b.Property(x => x.SubmittedAtUtc).HasColumnName("SubmittedAt");
        b.Property(x => x.StatusCode).HasColumnName("ApplicationStatusCode").HasMaxLength(30).IsUnicode(false).IsRequired();
        b.Property(x => x.CreatedAt).HasColumnName("CreatedAt"); b.Property(x => x.UpdatedAt).HasColumnName("UpdatedAt");
    }
}
