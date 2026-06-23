using Moe.SharedKernel.Domain;
using Moe.SharedKernel.Results;

namespace Moe.Modules.FasPayment.Domain.Payments;

internal sealed class PaymentCheckoutSession : Entity<long>
{
    private PaymentCheckoutSession() : base(0) { }

    private PaymentCheckoutSession(
        long billId,
        long courseEnrollmentId,
        long courseId,
        long personId,
        long paymentPlanId,
        decimal amount,
        int requiredInstallmentCount,
        string idempotencyKey,
        DateTime createdAtUtc) : base(0)
    {
        BillId = billId;
        CourseEnrollmentId = courseEnrollmentId;
        CourseId = courseId;
        PersonId = personId;
        CoursePaymentPlanId = paymentPlanId;
        Amount = amount;
        CurrencyCode = "SGD";
        RequiredInstallmentCount = requiredInstallmentCount;
        IdempotencyKey = idempotencyKey;
        CheckoutStatusCode = CheckoutStatusCodes.Pending;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = createdAtUtc;
    }

    public long BillId { get; private set; }
    public long CourseEnrollmentId { get; private set; }
    public long CourseId { get; private set; }
    public long PersonId { get; private set; }
    public long CoursePaymentPlanId { get; private set; }
    public long? PaymentId { get; private set; }
    public long? BillingStatementId { get; private set; }
    public decimal Amount { get; private set; }
    public string CurrencyCode { get; private set; } = string.Empty;
    public int RequiredInstallmentCount { get; private set; }
    public int PaidInstallmentCount { get; private set; }
    public string CheckoutStatusCode { get; private set; } = string.Empty;
    public string IdempotencyKey { get; private set; } = string.Empty;
    public string? ProviderCheckoutSessionId { get; private set; }
    public string? ProviderPaymentIntentId { get; private set; }
    public string? ProviderSubscriptionId { get; private set; }
    public string? ProviderSubscriptionScheduleId { get; private set; }
    public string? ProviderPriceId { get; private set; }
    public string? CheckoutUrl { get; private set; }
    public DateTime? ExpiresAtUtc { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
    public DateTime? LastPaymentEventAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public bool IsInstallment => RequiredInstallmentCount > 1;

    public static PaymentCheckoutSession CreateForStatement(
        long paymentId,
        long billingStatementId,
        long personId,
        decimal onlineAmount,
        DateTime createdAtUtc)
    {
        if (paymentId <= 0 || billingStatementId <= 0 || personId <= 0 || onlineAmount <= 0m)
            throw new ArgumentOutOfRangeException(nameof(paymentId));
        return new PaymentCheckoutSession
        {
            PaymentId = paymentId,
            BillingStatementId = billingStatementId,
            BillId = 0,
            CourseEnrollmentId = 0,
            CourseId = 0,
            PersonId = personId,
            CoursePaymentPlanId = 0,
            Amount = onlineAmount,
            CurrencyCode = "SGD",
            RequiredInstallmentCount = 1,
            CheckoutStatusCode = CheckoutStatusCodes.Pending,
            IdempotencyKey = $"statement-checkout:{billingStatementId}:{paymentId}",
            CreatedAtUtc = createdAtUtc,
            UpdatedAtUtc = createdAtUtc
        };
    }

    public static Result<PaymentCheckoutSession> Create(
        long billId,
        long courseEnrollmentId,
        long courseId,
        long personId,
        CoursePaymentPlan plan,
        decimal amount,
        DateTime createdAtUtc)
    {
        if (billId <= 0 || courseEnrollmentId <= 0 || courseId <= 0 || personId <= 0 || amount <= 0)
            return Result<PaymentCheckoutSession>.Failure(PaymentDomainErrors.InvalidCheckout);

        string key = $"stripe-checkout:{billId}:{plan.Id}:{personId}";
        return Result<PaymentCheckoutSession>.Success(new(
            billId,
            courseEnrollmentId,
            courseId,
            personId,
            plan.Id,
            amount,
            1,
            key,
            createdAtUtc));
    }

    public void AssignProviderCheckout(
        string sessionId,
        string priceId,
        string checkoutUrl,
        DateTime expiresAtUtc,
        DateTime updatedAtUtc)
    {
        ProviderCheckoutSessionId = Required(sessionId);
        ProviderPriceId = Required(priceId);
        CheckoutUrl = Required(checkoutUrl);
        ExpiresAtUtc = expiresAtUtc;
        UpdatedAtUtc = updatedAtUtc;
    }

    public bool CanResume(DateTime utcNow) =>
        CheckoutStatusCode == CheckoutStatusCodes.Pending &&
        !string.IsNullOrWhiteSpace(CheckoutUrl) &&
        ExpiresAtUtc is DateTime expiresAtUtc &&
        expiresAtUtc > utcNow;

    public void AttachPaymentIntent(string paymentIntentId, DateTime updatedAtUtc)
    {
        ProviderPaymentIntentId = Required(paymentIntentId);
        UpdatedAtUtc = updatedAtUtc;
    }

    public void AttachSubscription(string subscriptionId, string scheduleId, DateTime updatedAtUtc)
    {
        ProviderSubscriptionId = Required(subscriptionId);
        ProviderSubscriptionScheduleId = Required(scheduleId);
        UpdatedAtUtc = updatedAtUtc;
    }

    public bool CancelBeforePayment(DateTime updatedAtUtc)
    {
        if (PaidInstallmentCount > 0 ||
            CheckoutStatusCode is CheckoutStatusCodes.Active or CheckoutStatusCodes.PaidInFull)
            return false;

        CheckoutStatusCode = CheckoutStatusCodes.Cancelled;
        UpdatedAtUtc = updatedAtUtc;
        return true;
    }

    public bool ExpireBeforePayment(DateTime updatedAtUtc)
    {
        if (PaidInstallmentCount > 0 ||
            CheckoutStatusCode is CheckoutStatusCodes.Active or CheckoutStatusCodes.PaidInFull)
            return false;

        CheckoutStatusCode = CheckoutStatusCodes.Expired;
        UpdatedAtUtc = updatedAtUtc;
        return true;
    }

    public bool RecordSuccessfulPayment(DateTime updatedAtUtc)
    {
        if (LastPaymentEventAtUtc is DateTime lastEvent && updatedAtUtc < lastEvent) return false;
        if (CheckoutStatusCode == CheckoutStatusCodes.PaidInFull) return false;
        PaidInstallmentCount = Math.Min(PaidInstallmentCount + 1, RequiredInstallmentCount);
        CheckoutStatusCode = PaidInstallmentCount >= RequiredInstallmentCount
            ? CheckoutStatusCodes.PaidInFull
            : CheckoutStatusCodes.Active;
        UpdatedAtUtc = updatedAtUtc;
        LastPaymentEventAtUtc = updatedAtUtc;
        return true;
    }

    public bool RecordPaymentFailure(DateTime updatedAtUtc)
    {
        if (LastPaymentEventAtUtc is DateTime lastEvent && updatedAtUtc < lastEvent) return false;
        if (CheckoutStatusCode == CheckoutStatusCodes.PaidInFull) return false;
        CheckoutStatusCode = CheckoutStatusCodes.PaymentPastDue;
        UpdatedAtUtc = updatedAtUtc;
        LastPaymentEventAtUtc = updatedAtUtc;
        return true;
    }

    private static string Required(string value) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("Provider identifier is required.") : value.Trim();
}

public static class CheckoutStatusCodes
{
    public const string Pending = "PENDING";
    public const string Active = "ACTIVE";
    public const string PaymentPastDue = "PAYMENT_PAST_DUE";
    public const string PaidInFull = "PAID_IN_FULL";
    public const string Cancelled = "CANCELLED";
    public const string Expired = "EXPIRED";
}

public static class PaymentCheckoutPolicy
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(30);
}
