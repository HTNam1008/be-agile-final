using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Moe.Modules.EducationAccountTopUp.Domain.Lifecycle;

namespace Moe.Modules.EducationAccountTopUp.Infrastructure.Persistence;

internal sealed class EducationAccountLifecycleRunConfiguration
    : IEntityTypeConfiguration<EducationAccountLifecycleRun>
{
    public void Configure(EntityTypeBuilder<EducationAccountLifecycleRun> builder)
    {
        builder.ToTable("EducationAccountLifecycleRun", "account");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("EducationAccountLifecycleRunId").UseIdentityColumn();
        builder.Property(x => x.TriggerTypeCode).HasMaxLength(30).IsUnicode(false).IsRequired();
        builder.Property(x => x.StatusCode).HasMaxLength(30).IsUnicode(false).IsRequired();
        builder.Property(x => x.ErrorMessage).HasMaxLength(2000);
        builder.HasIndex(x => new { x.RunDateUtc, x.TriggerTypeCode })
            .IsUnique()
            .HasFilter("[TriggerTypeCode] = 'SCHEDULED'");
        builder.HasIndex(x => x.StartedAtUtc);
        builder.Ignore(x => x.DomainEvents);
        builder.Navigation(x => x.Items).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.HasMany(x => x.Items)
            .WithOne()
            .HasForeignKey(x => x.EducationAccountLifecycleRunId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class EducationAccountLifecycleRunItemConfiguration
    : IEntityTypeConfiguration<EducationAccountLifecycleRunItem>
{
    public void Configure(EntityTypeBuilder<EducationAccountLifecycleRunItem> builder)
    {
        builder.ToTable("EducationAccountLifecycleRunItem", "account");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("EducationAccountLifecycleRunItemId").UseIdentityColumn();
        builder.Property(x => x.ActionCode).HasMaxLength(30).IsUnicode(false).IsRequired();
        builder.HasIndex(x => x.EducationAccountLifecycleRunId);
        builder.HasIndex(x => x.PersonId);
        builder.HasIndex(x => x.EducationAccountId);
        builder.HasOne<Moe.Modules.EducationAccountTopUp.Domain.EducationAccounts.EducationAccount>()
            .WithMany()
            .HasForeignKey(x => x.EducationAccountId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
