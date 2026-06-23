using Moe.SharedKernel.Domain;
using Moe.SharedKernel.Results;

namespace Moe.Modules.FasPayment.Domain.Payments;

internal sealed class EnrollmentRefund : Entity<long>
{
    private EnrollmentRefund() : base(0) { }

    private EnrollmentRefund(
        long courseEnrollmentId,
        long personId,
        decimal paidAmount,
        decimal refundPercentage,
        decimal refundAmount,
        decimal educationAccountRefundAmount,
        decimal onlineRefundAmount,
        string policyPeriodCode,
        string idempotencyKey,
        long requestedByUserAccountId,
        DateTime requestedAtUtc) : base(0)
    {
        CourseEnrollmentId = courseEnrollmentId;
        PersonId = personId;
        PaidAmount = paidAmount;
        RefundPercentage = refundPercentage;
        RefundAmount = refundAmount;
        EducationAccountRefundAmount = educationAccountRefundAmount;
        OnlineRefundAmount = onlineRefundAmount;
        PolicyPeriodCode = policyPeriodCode;
        IdempotencyKey = idempotencyKey.Trim();
        RequestedByUserAccountId = requestedByUserAccountId;
        RequestedAtUtc = requestedAtUtc;
        RefundStatusCode = EnrollmentRefundStatusCodes.Pending;
    }

    public long CourseEnrollmentId { get; private set; }
    public long PersonId { get; private set; }
    public decimal PaidAmount { get; private set; }
    public decimal RefundPercentage { get; private set; }
    public decimal RefundAmount { get; private set; }
    public decimal EducationAccountRefundAmount { get; private set; }
    public decimal OnlineRefundAmount { get; private set; }
    public string PolicyPeriodCode { get; private set; } = string.Empty;
    public string RefundStatusCode { get; private set; } = string.Empty;
    public string IdempotencyKey { get; private set; } = string.Empty;
    public long RequestedByUserAccountId { get; private set; }
    public DateTime RequestedAtUtc { get; private set; }
    public DateTime? CompletedAtUtc { get; private set; }
    public string? FailureReason { get; private set; }

    public static Result<EnrollmentRefund> Create(
        long courseEnrollmentId,
        long personId,
        decimal paidAmount,
        decimal refundPercentage,
        decimal refundAmount,
        decimal educationAccountRefundAmount,
        decimal onlineRefundAmount,
        string policyPeriodCode,
        string idempotencyKey,
        long requestedByUserAccountId,
        DateTime requestedAtUtc)
    {
        if (courseEnrollmentId <= 0 || personId <= 0 || paidAmount < 0m ||
            refundPercentage is < 0m or > 100m || refundAmount < 0m ||
            educationAccountRefundAmount < 0m || onlineRefundAmount < 0m ||
            educationAccountRefundAmount + onlineRefundAmount != refundAmount ||
            string.IsNullOrWhiteSpace(policyPeriodCode) ||
            string.IsNullOrWhiteSpace(idempotencyKey) ||
            requestedByUserAccountId <= 0)
        {
            return Result<EnrollmentRefund>.Failure(PaymentDomainErrors.InvalidRefund);
        }

        return Result<EnrollmentRefund>.Success(new(
            courseEnrollmentId,
            personId,
            paidAmount,
            refundPercentage,
            refundAmount,
            educationAccountRefundAmount,
            onlineRefundAmount,
            policyPeriodCode,
            idempotencyKey,
            requestedByUserAccountId,
            requestedAtUtc));
    }

    public void MarkSucceeded(DateTime completedAtUtc)
    {
        RefundStatusCode = EnrollmentRefundStatusCodes.Succeeded;
        CompletedAtUtc = completedAtUtc;
        FailureReason = null;
    }

    public void MarkFailed(string failureReason)
    {
        RefundStatusCode = EnrollmentRefundStatusCodes.Failed;
        FailureReason = failureReason.Trim();
    }
}

internal sealed class EnrollmentRefundPart : Entity<long>
{
    private EnrollmentRefundPart() : base(0) { }

    public long EnrollmentRefundId { get; private set; }
    public long? PaymentId { get; private set; }
    public long? PaymentPartId { get; private set; }
    public string RefundMethodCode { get; private set; } = string.Empty;
    public decimal RefundAmount { get; private set; }
    public string RefundStatusCode { get; private set; } = string.Empty;
    public string? ProviderRefundId { get; private set; }
    public long? AccountTransactionId { get; private set; }
    public string IdempotencyKey { get; private set; } = string.Empty;
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? CompletedAtUtc { get; private set; }
    public string? FailureReason { get; private set; }

    public static EnrollmentRefundPart Create(
        long enrollmentRefundId,
        long? paymentId,
        long? paymentPartId,
        string refundMethodCode,
        decimal refundAmount,
        string idempotencyKey,
        DateTime createdAtUtc)
    {
        if (enrollmentRefundId <= 0 || refundAmount <= 0m ||
            string.IsNullOrWhiteSpace(refundMethodCode) ||
            string.IsNullOrWhiteSpace(idempotencyKey))
        {
            throw new ArgumentOutOfRangeException(nameof(refundAmount));
        }

        return new()
        {
            EnrollmentRefundId = enrollmentRefundId,
            PaymentId = paymentId,
            PaymentPartId = paymentPartId,
            RefundMethodCode = refundMethodCode,
            RefundAmount = refundAmount,
            RefundStatusCode = EnrollmentRefundStatusCodes.Pending,
            IdempotencyKey = idempotencyKey.Trim(),
            CreatedAtUtc = createdAtUtc
        };
    }

    public void MarkStripeSucceeded(string providerRefundId, DateTime completedAtUtc)
    {
        ProviderRefundId = providerRefundId.Trim();
        RefundStatusCode = EnrollmentRefundStatusCodes.Succeeded;
        CompletedAtUtc = completedAtUtc;
    }

    public void MarkEducationAccountSucceeded(long accountTransactionId, DateTime completedAtUtc)
    {
        AccountTransactionId = accountTransactionId;
        RefundStatusCode = EnrollmentRefundStatusCodes.Succeeded;
        CompletedAtUtc = completedAtUtc;
    }

    public void MarkFailed(string failureReason)
    {
        RefundStatusCode = EnrollmentRefundStatusCodes.Failed;
        FailureReason = failureReason.Trim();
    }
}

internal static class EnrollmentRefundStatusCodes
{
    public const string Pending = "PENDING";
    public const string Succeeded = "SUCCEEDED";
    public const string Failed = "FAILED";
}

internal static class EnrollmentRefundMethodCodes
{
    public const string EducationAccount = "EDUCATION_ACCOUNT";
    public const string Stripe = "STRIPE";
}
