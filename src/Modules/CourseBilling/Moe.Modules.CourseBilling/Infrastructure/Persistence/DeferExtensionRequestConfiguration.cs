using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Moe.Modules.CourseBilling.Domain.Billing;

namespace Moe.Modules.CourseBilling.Infrastructure.Persistence;

internal sealed class DeferExtensionRequestConfiguration : IEntityTypeConfiguration<DeferExtensionRequest>
{
    public void Configure(EntityTypeBuilder<DeferExtensionRequest> builder)
    {
        builder.ToTable("DeferExtensionRequest", "billing");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .HasColumnName("DeferExtensionRequestId")
            .UseIdentityColumn();
        builder.Property(x => x.StatusCode)
            .HasMaxLength(20)
            .IsUnicode(false)
            .IsRequired();
        builder.HasIndex(x => new { x.BillId, x.StatusCode })
            .HasFilter("[StatusCode] = 'PENDING'");
        builder.HasIndex(x => new { x.OrganizationId, x.StatusCode, x.RequestedAtUtc });
    }
}
