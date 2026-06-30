using Moe.SharedKernel.Domain;
using Moe.SharedKernel.Results;

namespace Moe.Modules.FasPayment.Domain.Payments;

internal abstract partial class PaymentCheckoutSession : Entity<long>
{
    protected PaymentCheckoutSession() : base(0) { }

    protected PaymentCheckoutSession(
        string checkoutSessionTypeCode,
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
        CheckoutSessionTypeCode = checkoutSessionTypeCode;
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

    public string CheckoutSessionTypeCode { get; protected set; } = string.Empty;
    public long BillId { get; protected set; }
    public long CourseEnrollmentId { get; protected set; }
    public long CourseId { get; protected set; }
    public long PersonId { get; protected set; }
    public long CoursePaymentPlanId { get; protected set; }
    public long? PaymentId { get; protected set; }
    public long? BillingStatementId { get; protected set; }
    public decimal Amount { get; protected set; }
    public string CurrencyCode { get; protected set; } = string.Empty;
    public int RequiredInstallmentCount { get; protected set; }
    public int PaidInstallmentCount { get; protected set; }
    public string CheckoutStatusCode { get; protected set; } = string.Empty;
    public string IdempotencyKey { get; protected set; } = string.Empty;
    public string? ProviderCheckoutSessionId { get; private set; }
    public string? ProviderPaymentIntentId { get; private set; }
    public string? ProviderSubscriptionId { get; private set; }
    public string? ProviderSubscriptionScheduleId { get; private set; }
    public string? ProviderPriceId { get; private set; }
    public string? CheckoutUrl { get; private set; }
    public DateTime? ExpiresAtUtc { get; private set; }
    public DateTime CreatedAtUtc { get; protected set; }
    public DateTime UpdatedAtUtc { get; protected set; }
    public DateTime? LastPaymentEventAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public bool IsInstallment => RequiredInstallmentCount > 1;
    public bool IsBillCheckout => CheckoutSessionTypeCode == PaymentCheckoutSessionTypeCodes.Bill;
    public bool IsStatementCheckout => CheckoutSessionTypeCode == PaymentCheckoutSessionTypeCodes.Statement;

    protected static string Required(string value) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("Provider identifier is required.") : value.Trim();
}

internal sealed class StatementPaymentCheckoutSession : PaymentCheckoutSession
{
    private StatementPaymentCheckoutSession() : base() { }

    public static StatementPaymentCheckoutSession Create(
        long paymentId,
        long billingStatementId,
        long personId,
        decimal onlineAmount,
        DateTime createdAtUtc)
    {
        if (paymentId <= 0 || billingStatementId <= 0 || personId <= 0 || onlineAmount <= 0m)
            throw new ArgumentOutOfRangeException(nameof(paymentId));
        return new StatementPaymentCheckoutSession
        {
            CheckoutSessionTypeCode = PaymentCheckoutSessionTypeCodes.Statement,
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
}

internal sealed class BillPaymentCheckoutSession : PaymentCheckoutSession
{
    private BillPaymentCheckoutSession() : base() { }

    private BillPaymentCheckoutSession(
        long billId,
        long courseEnrollmentId,
        long courseId,
        long personId,
        long paymentPlanId,
        decimal amount,
        int requiredInstallmentCount,
        string idempotencyKey,
        DateTime createdAtUtc) : base(
            PaymentCheckoutSessionTypeCodes.Bill,
            billId,
            courseEnrollmentId,
            courseId,
            personId,
            paymentPlanId,
            amount,
            requiredInstallmentCount,
            idempotencyKey,
            createdAtUtc)
    {
    }

    public static Result<BillPaymentCheckoutSession> Create(
        long billId,
        long courseEnrollmentId,
        long courseId,
        long personId,
        CoursePaymentPlan plan,
        decimal amount,
        DateTime createdAtUtc)
    {
        if (billId <= 0 || courseEnrollmentId <= 0 || courseId <= 0 || personId <= 0 || amount <= 0)
            return Result<BillPaymentCheckoutSession>.Failure(PaymentDomainErrors.InvalidCheckout);

        string key = $"stripe-checkout:{billId}:{plan.Id}:{personId}";
        return Result<BillPaymentCheckoutSession>.Success(new(
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
}

internal abstract partial class PaymentCheckoutSession
{
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
}

public static class PaymentCheckoutSessionTypeCodes
{
    public const string Bill = "BILL";
    public const string Statement = "STATEMENT";
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
