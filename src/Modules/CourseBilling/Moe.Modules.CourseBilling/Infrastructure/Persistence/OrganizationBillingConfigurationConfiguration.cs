using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Moe.Modules.CourseBilling.Domain.Billing;

namespace Moe.Modules.CourseBilling.Infrastructure.Persistence;

internal sealed class OrganizationBillingConfigurationConfiguration
    : IEntityTypeConfiguration<OrganizationBillingConfiguration>
{
    public void Configure(EntityTypeBuilder<OrganizationBillingConfiguration> builder)
    {
        builder.ToTable("OrganizationBillingConfiguration", "billing");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .HasColumnName("OrganizationBillingConfigurationId")
            .UseIdentityColumn();
        builder.HasIndex(x => x.OrganizationId).IsUnique();
        builder.Property(x => x.CreatedAtUtc).HasColumnName("CreatedAt");
        builder.Property(x => x.UpdatedAtUtc).HasColumnName("UpdatedAt");
    }
}
