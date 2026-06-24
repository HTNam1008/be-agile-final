using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Moe.Modules.EducationAccountTopUp.Domain.EducationAccounts;

namespace Moe.Modules.EducationAccountTopUp.Infrastructure.Persistence;

public sealed class EducationAccountConfiguration : IEntityTypeConfiguration<EducationAccount>
{
    public void Configure(EntityTypeBuilder<EducationAccount> builder)
    {
        builder.ToTable("EducationAccount", "account");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("EducationAccountId").UseIdentityColumn();
        builder.HasIndex(x => x.PersonId).IsUnique();
        builder.HasIndex(x => x.AccountNumber).IsUnique();
        builder.Property(x => x.AccountNumber).HasMaxLength(50).IsRequired();
        builder.Property(x => x.StatusCode).HasColumnName("AccountStatusCode").HasMaxLength(30).IsUnicode(false).IsRequired();
        builder.Property(x => x.OpenedAtUtc).HasColumnName("OpenedAt");
        builder.Property(x => x.OpeningModeCode).HasColumnName("OpeningTypeCode").HasMaxLength(30).IsUnicode(false).IsRequired();
        builder.Property(x => x.OpeningReasonCode).HasMaxLength(50).IsUnicode(false).IsRequired();
        builder.Property(x => x.OpeningRemarks).HasColumnName("OpeningReason").HasMaxLength(1000);
        builder.Property(x => x.OpenedByUserId).HasColumnName("OpenedByLoginAccountId");
        builder.Property(x => x.PendingClosureAtUtc).HasColumnName("PendingClosureAt");
        builder.Property(x => x.ClosureExceptionReason).HasMaxLength(1000);
        builder.Property(x => x.ClosedAtUtc).HasColumnName("ClosedAt");
        builder.Property(x => x.ClosingTypeCode).HasMaxLength(30).IsUnicode(false);
        builder.Property(x => x.ClosingReasonCode).HasColumnName("ClosingReasonCode").HasMaxLength(50).IsUnicode(false);
        builder.Property(x => x.ClosingRemarks).HasColumnName("ClosingReason").HasMaxLength(1000);
        builder.Property(x => x.ClosedByLoginAccountId).HasColumnName("ClosedByLoginAccountId");
        builder.Property(x => x.CachedBalance).HasColumnName("CurrentBalance").HasPrecision(19, 2);
        builder.Property(x => x.RowVersion).IsRowVersion();
        builder.Ignore(x => x.DomainEvents);
        builder.HasData(AccountDemoSeedData.DemoStudentAccounts.Select(Seed));
    }

    private static object Seed(DemoEducationAccountSeed account)
        => new
        {
            Id = account.EducationAccountId,
            PersonId = account.PersonId,
            AccountNumber = account.AccountNumber,
            StatusCode = AccountStatuses.Active,
            OpenedAtUtc = AccountDemoSeedData.SeededAtUtc,
            OpeningModeCode = AccountOpeningModeCodes.Manual,
            OpeningReasonCode = "MANUAL_LEGACY",
            OpeningRemarks = "Demo seeded account for top-up search",
            OpenedByUserId = (long?)AccountDemoSeedData.HqAdminLoginAccountId,
            PendingClosureAtUtc = (DateTimeOffset?)null,
            ClosureExceptionUntil = (DateOnly?)null,
            ClosureExceptionReason = (string?)null,
            ClosureExceptionApprovedByLoginAccountId = (long?)null,
            ClosedAtUtc = (DateTimeOffset?)null,
            ClosingTypeCode = (string?)null,
            ClosingReasonCode = (string?)null,
            ClosingRemarks = (string?)null,
            ClosedByLoginAccountId = (long?)null,
            CachedBalance = 0m
        };
}
