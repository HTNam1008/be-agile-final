using Moe.SharedKernel.Domain;

namespace Moe.Modules.FasPayment.Domain.Payments;

internal sealed class Payment : Entity<long>
{
    private Payment() : base(0) { }

    private Payment(
        long billId,
        long payerPersonId,
        decimal amount,
        string idempotencyKey,
        string providerPaymentIntentId,
        string? providerInvoiceId,
        int installmentNumber,
        DateTime initiatedAtUtc) : base(0)
    {
        BillId = billId;
        PayerPersonId = payerPersonId;
        PaymentNumber = $"PAY-{initiatedAtUtc:yyyyMMdd}-{Guid.NewGuid():N}"[..30].ToUpperInvariant();
        PaymentAmount = amount;
        SuccessfulAmount = amount;
        PaymentStatusCode = PaymentStatusCodes.Completed;
        InitiatedAtUtc = initiatedAtUtc;
        CompletedAtUtc = initiatedAtUtc;
        IdempotencyKey = idempotencyKey;
        ReceiptNumber = $"RCT-{initiatedAtUtc:yyyyMMdd}-{Guid.NewGuid():N}"[..30].ToUpperInvariant();
        ProviderPaymentIntentId = providerPaymentIntentId;
        ProviderInvoiceId = providerInvoiceId;
        InstallmentNumber = installmentNumber;
    }

    public string PaymentNumber { get; private set; } = string.Empty;
    public long BillId { get; private set; }
    public long PayerPersonId { get; private set; }
    public decimal PaymentAmount { get; private set; }
    public decimal SuccessfulAmount { get; private set; }
    public string PaymentStatusCode { get; private set; } = string.Empty;
    public DateTime InitiatedAtUtc { get; private set; }
    public DateTime? CompletedAtUtc { get; private set; }
    public string IdempotencyKey { get; private set; } = string.Empty;
    public string? ReceiptNumber { get; private set; }
    public string? ProviderPaymentIntentId { get; private set; }
    public string? ProviderInvoiceId { get; private set; }
    public string? ProviderChargeId { get; private set; }
    public int InstallmentNumber { get; private set; }
    public long? BillingStatementId { get; private set; }
    public decimal EducationAccountAmount { get; private set; }
    public decimal OnlinePaymentAmount { get; private set; }
    public string PaymentModeCode { get; private set; } = PaymentModes.AutoEducationAccountThenOnline;
    public DateTime? FailedAtUtc { get; private set; }
    public DateTime? ExpiredAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public static Payment StartStatementPayment(
        long billingStatementId,
        long payerPersonId,
        decimal requestedAmount,
        decimal educationAccountAmount,
        decimal onlinePaymentAmount,
        string idempotencyKey,
        DateTime initiatedAtUtc)
    {
        if (billingStatementId <= 0 || payerPersonId <= 0 || requestedAmount <= 0m ||
            educationAccountAmount < 0m || onlinePaymentAmount < 0m ||
            educationAccountAmount + onlinePaymentAmount != requestedAmount)
            throw new ArgumentOutOfRangeException(nameof(requestedAmount));
        return new Payment
        {
            BillId = 0,
            BillingStatementId = billingStatementId,
            PayerPersonId = payerPersonId,
            PaymentNumber = $"PAY-{initiatedAtUtc:yyyyMMdd}-{Guid.NewGuid():N}"[..30].ToUpperInvariant(),
            PaymentAmount = requestedAmount,
            SuccessfulAmount = 0m,
            EducationAccountAmount = educationAccountAmount,
            OnlinePaymentAmount = onlinePaymentAmount,
            PaymentModeCode = PaymentModes.AutoEducationAccountThenOnline,
            PaymentStatusCode = onlinePaymentAmount > 0m
                ? PaymentStatusCodes.PendingOnlinePayment
                : PaymentStatusCodes.Initiated,
            InitiatedAtUtc = initiatedAtUtc,
            UpdatedAtUtc = initiatedAtUtc,
            IdempotencyKey = idempotencyKey
        };
    }

    public void MarkSuccessful(DateTime completedAtUtc)
    {
        SuccessfulAmount = PaymentAmount;
        PaymentStatusCode = PaymentStatusCodes.Successful;
        CompletedAtUtc = completedAtUtc;
        UpdatedAtUtc = completedAtUtc;
        ReceiptNumber ??= $"RCT-{completedAtUtc:yyyyMMdd}-{Guid.NewGuid():N}"[..30].ToUpperInvariant();
    }

    public void MarkFailed(DateTime failedAtUtc)
    {
        PaymentStatusCode = PaymentStatusCodes.Failed;
        FailedAtUtc = failedAtUtc;
        UpdatedAtUtc = failedAtUtc;
    }

    public void MarkCancelled(DateTime cancelledAtUtc)
    {
        if (PaymentStatusCode == PaymentStatusCodes.Successful) return;
        PaymentStatusCode = PaymentStatusCodes.Cancelled;
        FailedAtUtc = cancelledAtUtc;
        UpdatedAtUtc = cancelledAtUtc;
    }

    public static Payment RecordProviderSuccess(
        long billId,
        long payerPersonId,
        decimal amount,
        string providerPaymentIntentId,
        string? providerInvoiceId,
        string? providerChargeId,
        int installmentNumber,
        DateTime initiatedAtUtc)
    {
        string idempotencyKey = providerInvoiceId is null
            ? $"STRIPE-PI:{providerPaymentIntentId}"
            : $"STRIPE-INVOICE:{providerInvoiceId}";
        Payment payment = new(
            billId,
            payerPersonId,
            amount,
            idempotencyKey,
            providerPaymentIntentId,
            providerInvoiceId,
            installmentNumber,
            initiatedAtUtc);
        payment.ProviderChargeId = string.IsNullOrWhiteSpace(providerChargeId) ? null : providerChargeId.Trim();
        return payment;
    }

    public void ApplyProviderRefundTotal(decimal refundedAmount)
    {
        decimal normalized = decimal.Round(refundedAmount, 2, MidpointRounding.AwayFromZero);
        if (normalized <= 0m || normalized > PaymentAmount)
            throw new ArgumentOutOfRangeException(nameof(refundedAmount));
        SuccessfulAmount = PaymentAmount - normalized;
        PaymentStatusCode = SuccessfulAmount == 0m
            ? PaymentStatusCodes.Refunded
            : PaymentStatusCodes.PartiallyRefunded;
    }
}

public static class PaymentStatusCodes
{
    public const string Pending = "PENDING";
    public const string Completed = "COMPLETED";
    public const string Failed = "FAILED";
    public const string PartiallyRefunded = "PARTIALLY_REFUNDED";
    public const string Refunded = "REFUNDED";
    public const string Initiated = "INITIATED";
    public const string PendingOnlinePayment = "PENDING_ONLINE_PAYMENT";
    public const string Successful = "SUCCESSFUL";
    public const string Expired = "EXPIRED";
    public const string Cancelled = "CANCELLED";
}
