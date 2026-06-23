using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Moe.Modules.FasPayment.Domain.Payments;

namespace Moe.Modules.FasPayment.Infrastructure.Persistence;

internal sealed class CoursePaymentPlanConfiguration : IEntityTypeConfiguration<CoursePaymentPlan>
{
    public void Configure(EntityTypeBuilder<CoursePaymentPlan> builder)
    {
        builder.ToTable("CoursePaymentPlan", "payment");
        builder.HasKey(plan => plan.Id);
        builder.Property(plan => plan.Id).HasColumnName("CoursePaymentPlanId").UseIdentityColumn();
        builder.HasIndex(plan => new { plan.CourseId, plan.Version }).IsUnique();
        builder.HasIndex(plan => new { plan.CourseId, plan.IsActive });
        builder.Property(plan => plan.DisplayName).HasMaxLength(100).IsRequired();
        builder.Property(plan => plan.PlanTypeCode).HasMaxLength(30).IsUnicode(false).IsRequired();
        builder.Property(plan => plan.CurrencyCode).HasMaxLength(3).IsUnicode(false).IsRequired();
        builder.Property(plan => plan.CreatedAtUtc).HasColumnName("CreatedAt");
        builder.Property(plan => plan.UpdatedAtUtc).HasColumnName("UpdatedAt");
        builder.Property(plan => plan.RowVersion).IsRowVersion();
    }
}

internal sealed class PaymentCheckoutSessionConfiguration : IEntityTypeConfiguration<PaymentCheckoutSession>
{
    public void Configure(EntityTypeBuilder<PaymentCheckoutSession> builder)
    {
        builder.ToTable("PaymentCheckoutSession", "payment");
        builder.HasKey(checkout => checkout.Id);
        builder.Property(checkout => checkout.Id).HasColumnName("PaymentCheckoutSessionId").UseIdentityColumn();
        builder.HasIndex(checkout => checkout.IdempotencyKey).IsUnique();
        builder.HasIndex(checkout => checkout.ProviderCheckoutSessionId).IsUnique().HasFilter("[ProviderCheckoutSessionId] IS NOT NULL");
        builder.HasIndex(checkout => checkout.ProviderPaymentIntentId).HasFilter("[ProviderPaymentIntentId] IS NOT NULL");
        builder.HasIndex(checkout => checkout.ProviderSubscriptionId).IsUnique().HasFilter("[ProviderSubscriptionId] IS NOT NULL");
        builder.HasIndex(checkout => new { checkout.BillId, checkout.PersonId });
        builder.HasIndex(checkout => checkout.PaymentId).IsUnique().HasFilter("[PaymentId] IS NOT NULL");
        builder.Property(checkout => checkout.Amount).HasPrecision(19, 2);
        builder.Property(checkout => checkout.CurrencyCode).HasMaxLength(3).IsUnicode(false).IsRequired();
        builder.Property(checkout => checkout.CheckoutStatusCode).HasMaxLength(30).IsUnicode(false).IsRequired();
        builder.Property(checkout => checkout.IdempotencyKey).HasMaxLength(150).IsRequired();
        builder.Property(checkout => checkout.ProviderCheckoutSessionId).HasMaxLength(100);
        builder.Property(checkout => checkout.ProviderPaymentIntentId).HasMaxLength(100);
        builder.Property(checkout => checkout.ProviderSubscriptionId).HasMaxLength(100);
        builder.Property(checkout => checkout.ProviderSubscriptionScheduleId).HasMaxLength(100);
        builder.Property(checkout => checkout.ProviderPriceId).HasMaxLength(100);
        builder.Property(checkout => checkout.CheckoutUrl).HasMaxLength(2048);
        builder.Property(checkout => checkout.ExpiresAtUtc).HasColumnName("ExpiresAt");
        builder.Property(checkout => checkout.CreatedAtUtc).HasColumnName("CreatedAt");
        builder.Property(checkout => checkout.UpdatedAtUtc).HasColumnName("UpdatedAt");
        builder.Property(checkout => checkout.LastPaymentEventAtUtc).HasColumnName("LastPaymentEventAt");
        builder.Property(checkout => checkout.RowVersion).IsRowVersion();
    }
}

internal sealed class ProcessedPaymentWebhookEventConfiguration : IEntityTypeConfiguration<ProcessedPaymentWebhookEvent>
{
    public void Configure(EntityTypeBuilder<ProcessedPaymentWebhookEvent> builder)
    {
        builder.ToTable("ProcessedWebhookEvent", "payment");
        builder.HasKey(webhookEvent => webhookEvent.Id);
        builder.Property(webhookEvent => webhookEvent.Id).HasColumnName("ProcessedWebhookEventId").UseIdentityColumn();
        builder.HasIndex(webhookEvent => webhookEvent.ProviderEventId).IsUnique();
        builder.Property(webhookEvent => webhookEvent.ProviderEventId).HasMaxLength(100).IsRequired();
        builder.Property(webhookEvent => webhookEvent.EventType).HasMaxLength(100).IsRequired();
        builder.Property(webhookEvent => webhookEvent.ProcessingStatusCode).HasMaxLength(30).IsUnicode(false).IsRequired();
        builder.Property(webhookEvent => webhookEvent.FailureMessage).HasMaxLength(1000);
        builder.Property(webhookEvent => webhookEvent.ReceivedAtUtc).HasColumnName("ReceivedAt");
        builder.Property(webhookEvent => webhookEvent.ProcessedAtUtc).HasColumnName("ProcessedAt");
    }
}

internal sealed class PaymentRefundConfiguration : IEntityTypeConfiguration<PaymentRefund>
{
    public void Configure(EntityTypeBuilder<PaymentRefund> builder)
    {
        builder.ToTable("PaymentRefund", "payment");
        builder.HasKey(refund => refund.Id);
        builder.Property(refund => refund.Id).HasColumnName("PaymentRefundId").UseIdentityColumn();
        builder.HasIndex(refund => refund.ProviderRefundId).IsUnique().HasFilter("[ProviderRefundId] IS NOT NULL");
        builder.HasIndex(refund => refund.PaymentId);
        builder.Property(refund => refund.Amount).HasPrecision(19, 2);
        builder.Property(refund => refund.Reason).HasMaxLength(500).IsRequired();
        builder.Property(refund => refund.RefundStatusCode).HasMaxLength(30).IsUnicode(false).IsRequired();
        builder.Property(refund => refund.ProviderRefundId).HasMaxLength(100);
        builder.Property(refund => refund.RequestedAtUtc).HasColumnName("RequestedAt");
        builder.Property(refund => refund.CompletedAtUtc).HasColumnName("CompletedAt");
    }
}
