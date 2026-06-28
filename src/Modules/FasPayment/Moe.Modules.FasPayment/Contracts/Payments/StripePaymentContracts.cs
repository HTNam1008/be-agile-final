namespace Moe.Modules.FasPayment.Contracts.Payments;

public sealed record CreateCoursePaymentPlanRequest(
    string DisplayName,
    string PlanTypeCode,
    int InstallmentCount);

public sealed record CoursePaymentPlanResponse(
    long CoursePaymentPlanId,
    long CourseId,
    string DisplayName,
    string PlanTypeCode,
    string CurrencyCode,
    int InstallmentCount,
    int Version,
    bool IsActive);

public sealed record CreateStripeCheckoutRequest(long BillId, long CoursePaymentPlanId);

public sealed record StripeCheckoutResponse(
    long PaymentCheckoutSessionId,
    string CheckoutUrl,
    string CheckoutStatusCode);

public sealed record PaymentCheckoutStatusResponse(
    long PaymentCheckoutSessionId,
    long BillId,
    string CheckoutStatusCode,
    int PaidInstallmentCount,
    int RequiredInstallmentCount,
    string CheckoutSessionTypeCode,
    long? PaymentId,
    long? BillingStatementId,
    decimal Amount,
    string CurrencyCode,
    string? PaymentStatusCode,
    decimal? EducationAccountAmount,
    decimal? OnlinePaymentAmount,
    IReadOnlyCollection<long> BillIds,
    string? CheckoutUrl,
    DateTime? ExpiresAtUtc,
    bool CanResume);

public sealed record CreatePaymentRefundRequest(decimal Amount, string Reason);
public sealed record PaymentRefundResponse(long PaymentRefundId, long PaymentId, decimal Amount, string RefundStatusCode);
public sealed record AdminPaymentResponse(
    long PaymentId,
    long BillId,
    long PayerPersonId,
    decimal PaymentAmount,
    decimal SuccessfulAmount,
    string PaymentStatusCode,
    string? ProviderChargeId,
    DateTime InitiatedAtUtc);
public sealed record PaymentWebhookEventResponse(
    long ProcessedWebhookEventId,
    string ProviderEventId,
    string EventType,
    string ProcessingStatusCode,
    DateTime ReceivedAtUtc);
