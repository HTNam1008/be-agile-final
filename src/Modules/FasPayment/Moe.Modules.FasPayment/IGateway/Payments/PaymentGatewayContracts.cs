using Moe.Modules.FasPayment.Domain.Payments;
using Moe.Modules.FasPayment.Contracts.Payments;
using Moe.SharedKernel.Results;

namespace Moe.Modules.FasPayment.IGateway.Payments;

internal interface IPaymentCheckoutRepository
{
    Task<int> GetNextPlanVersionAsync(long courseId, CancellationToken cancellationToken);
    Task AddPlanAsync(CoursePaymentPlan plan, CancellationToken cancellationToken);
    Task<CoursePaymentPlan?> FindPlanAsync(long planId, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<CoursePaymentPlan>> ListActivePlansAsync(long courseId, CancellationToken cancellationToken);
    Task<PaymentCheckoutSession?> FindOpenCheckoutAsync(long billId, long personId, CancellationToken cancellationToken);
    Task<PaymentCheckoutSession?> FindCheckoutAsync(long checkoutId, long personId, CancellationToken cancellationToken);
    Task<PaymentCheckoutSession?> FindCheckoutAsync(long checkoutId, CancellationToken cancellationToken);
    Task<PaymentCheckoutSession?> FindCheckoutByProviderSessionAsync(string providerSessionId, CancellationToken cancellationToken);
    Task<PaymentCheckoutSession?> FindCheckoutBySubscriptionAsync(string providerSubscriptionId, CancellationToken cancellationToken);
    Task AddCheckoutAsync(PaymentCheckoutSession checkout, CancellationToken cancellationToken);
    Task AddStatementPaymentAsync(
        Payment payment,
        IReadOnlyCollection<PaymentPart> parts,
        IReadOnlyCollection<PaymentAllocation> allocations,
        CancellationToken cancellationToken);
    Task<IReadOnlyCollection<PaymentPart>> ListPaymentPartsAsync(long paymentId, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<PaymentAllocation>> ListPaymentAllocationsAsync(long paymentId, CancellationToken cancellationToken);
    Task<bool> PaymentReferenceExistsAsync(string providerReference, CancellationToken cancellationToken);
    Task AddPaymentAsync(Payment payment, CancellationToken cancellationToken);
    Task<Payment?> FindPaymentAsync(long paymentId, CancellationToken cancellationToken);
    Task<Payment?> FindActiveStatementPaymentAsync(
        long billingStatementId,
        long personId,
        CancellationToken cancellationToken);
    Task<IReadOnlyCollection<Payment>> ListActiveStatementPaymentsAsync(
        long billingStatementId,
        long personId,
        long excludingPaymentId,
        CancellationToken cancellationToken);
    Task<PaymentCheckoutSession?> FindCheckoutByPaymentAsync(
        long paymentId,
        CancellationToken cancellationToken);
    Task<Payment?> FindPaymentByChargeAsync(string providerChargeId, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<Payment>> ListPaymentsAsync(CancellationToken cancellationToken);
    Task<IReadOnlyCollection<Payment>> ListPaymentsForPersonAsync(
        long personId,
        CancellationToken cancellationToken);
    Task<decimal> GetSucceededRefundAmountAsync(long paymentId, CancellationToken cancellationToken);
    Task AddRefundAsync(PaymentRefund refund, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<PaymentRefund>> ListRefundsAsync(long paymentId, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<ProcessedPaymentWebhookEvent>> ListWebhookEventsAsync(CancellationToken cancellationToken);
    Task<bool> WebhookEventExistsAsync(string providerEventId, CancellationToken cancellationToken);
    Task AddWebhookEventAsync(ProcessedPaymentWebhookEvent webhookEvent, CancellationToken cancellationToken);
    Task ExecuteInTransactionAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken);
}

internal interface IPaymentPersistenceTracker
{
    bool IsRetryablePersistenceConflict(Exception exception);
    string DescribeConflict(Exception exception);
    void ClearTrackedChanges();
}

internal interface ILegacyCoursePaymentGateway
{
    Task<OutstandingBillsResponse> ReadOutstandingBillsAsync(long personId, CancellationToken cancellationToken);
    Task<Result<PayBillResponse>> PayBillAsync(
        long personId,
        long? userAccountId,
        PayBillRequest request,
        CancellationToken cancellationToken);
}

internal sealed record StripeCheckoutGatewayRequest(
    string IdempotencyKey,
    long CheckoutId,
    long CourseId,
    long BillId,
    string CourseName,
    string CurrencyCode,
    long UnitAmountMinor,
    int InstallmentCount,
    string? ProviderPriceId,
    DateTime ExpiresAtUtc);

internal sealed record StripeCheckoutGatewayResult(
    string ProviderSessionId,
    string ProviderPriceId,
    string CheckoutUrl,
    DateTime ExpiresAtUtc);

internal sealed record StripeScheduleGatewayResult(string ProviderScheduleId);

internal enum PaymentWebhookKind
{
    CheckoutCompleted,
    CheckoutExpired,
    PaymentSucceeded,
    PaymentFailed,
    InvoicePaid,
    InvoicePaymentFailed,
    SubscriptionDeleted,
    ChargeRefunded,
    Ignored
}

internal sealed record ParsedPaymentWebhook(
    string ProviderEventId,
    string EventType,
    PaymentWebhookKind Kind,
    DateTime CreatedAtUtc,
    long CheckoutId,
    string? ProviderCheckoutSessionId,
    string? ProviderPaymentIntentId,
    string? ProviderInvoiceId,
    string? ProviderChargeId,
    string? ProviderSubscriptionId,
    long AmountMinor,
    string CurrencyCode);

internal sealed record StripeRefundGatewayResult(string ProviderRefundId);

internal interface IStripePaymentGateway
{
    Task<StripeCheckoutGatewayResult> CreateCheckoutAsync(
        StripeCheckoutGatewayRequest request,
        CancellationToken cancellationToken);

    Task ExpireCheckoutAsync(
        string providerSessionId,
        CancellationToken cancellationToken);

    Task<StripeScheduleGatewayResult> AttachFiniteScheduleAsync(
        string providerSubscriptionId,
        string providerPriceId,
        int installmentCount,
        CancellationToken cancellationToken);

    ParsedPaymentWebhook ParseWebhook(string payload, string signatureHeader);
    Task<StripeRefundGatewayResult> CreateRefundAsync(
        string idempotencyKey,
        string providerChargeId,
        long amountMinor,
        CancellationToken cancellationToken);
}

internal sealed class InvalidPaymentWebhookException : Exception
{
    public InvalidPaymentWebhookException() : base("The payment webhook is invalid.") { }
}

internal sealed class PaymentProviderUnavailableException : Exception
{
    public PaymentProviderUnavailableException() : base("The payment provider is unavailable.") { }
}
