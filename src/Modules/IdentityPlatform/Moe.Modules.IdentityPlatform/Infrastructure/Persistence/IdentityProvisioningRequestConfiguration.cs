using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Moe.Modules.IdentityPlatform.Domain.Iam;

namespace Moe.Modules.IdentityPlatform.Infrastructure.Persistence;

internal sealed class IdentityProvisioningRequestConfiguration : IEntityTypeConfiguration<IdentityProvisioningRequest>
{
    public void Configure(EntityTypeBuilder<IdentityProvisioningRequest> builder)
    {
        builder.ToTable("IdentityProvisioningRequest", "iam");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("IdentityProvisioningRequestId").UseIdentityColumn();
        builder.HasIndex(x => x.IdempotencyKey).IsUnique();
        builder.HasIndex(x => new { x.PersonId, x.IdentityProviderCode })
            .IsUnique()
            .HasFilter("[ProvisioningStatusCode] IN ('PENDING', 'COMPLETED')");
        builder.Property(x => x.IdentityProviderCode).HasMaxLength(40).IsUnicode(false).IsRequired();
        builder.Property(x => x.ExternalIssuer).HasMaxLength(300).IsRequired();
        builder.Property(x => x.RequestedEmailNormalized).HasMaxLength(320);
        builder.Property(x => x.DisplayNameSnapshot).HasMaxLength(200).IsRequired();
        builder.Property(x => x.ProvisioningStatusCode).HasMaxLength(40).IsUnicode(false).IsRequired();
        builder.Property(x => x.IdempotencyKey).HasMaxLength(100).IsRequired();
        builder.Property(x => x.ExternalTenantId).HasMaxLength(100);
        builder.Property(x => x.ExternalObjectId).HasMaxLength(100);
        builder.Property(x => x.ExternalSubjectId).HasMaxLength(200);
        builder.Property(x => x.FailureCode).HasMaxLength(100).IsUnicode(false);
        builder.Property(x => x.FailureReason).HasMaxLength(1000);
        builder.Property(x => x.CorrelationId).HasMaxLength(100).IsRequired();
        builder.Property(x => x.RowVersion).IsRowVersion();
        builder.Ignore(x => x.DomainEvents);
    }
}
