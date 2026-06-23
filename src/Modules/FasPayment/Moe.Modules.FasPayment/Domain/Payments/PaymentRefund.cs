using Moe.SharedKernel.Domain;
using Moe.SharedKernel.Results;

namespace Moe.Modules.FasPayment.Domain.Payments;

internal sealed class PaymentRefund : Entity<long>
{
    private PaymentRefund() : base(0) { }

    private PaymentRefund(
        long paymentId,
        decimal amount,
        string reason,
        long requestedByUserAccountId,
        DateTime requestedAtUtc) : base(0)
    {
        PaymentId = paymentId;
        Amount = amount;
        Reason = reason.Trim();
        RequestedByUserAccountId = requestedByUserAccountId;
        RefundStatusCode = RefundStatusCodes.Pending;
        RequestedAtUtc = requestedAtUtc;
    }

    public long PaymentId { get; private set; }
    public decimal Amount { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public string RefundStatusCode { get; private set; } = string.Empty;
    public string? ProviderRefundId { get; private set; }
    public long RequestedByUserAccountId { get; private set; }
    public DateTime RequestedAtUtc { get; private set; }
    public DateTime? CompletedAtUtc { get; private set; }

    public static Result<PaymentRefund> Create(
        long paymentId,
        decimal amount,
        string reason,
        long requestedByUserAccountId,
        DateTime requestedAtUtc)
    {
        if (paymentId <= 0 || amount <= 0m || string.IsNullOrWhiteSpace(reason) || requestedByUserAccountId <= 0)
            return Result<PaymentRefund>.Failure(PaymentDomainErrors.InvalidRefund);
        return Result<PaymentRefund>.Success(new(paymentId, amount, reason, requestedByUserAccountId, requestedAtUtc));
    }

    public void AssignProviderRefund(string providerRefundId)
        => ProviderRefundId = string.IsNullOrWhiteSpace(providerRefundId)
            ? throw new ArgumentException("Provider refund identifier is required.")
            : providerRefundId.Trim();

    public void MarkSucceeded(DateTime completedAtUtc)
    {
        RefundStatusCode = RefundStatusCodes.Succeeded;
        CompletedAtUtc = completedAtUtc;
    }
}

public static class RefundStatusCodes
{
    public const string Pending = "PENDING";
    public const string Succeeded = "SUCCEEDED";
    public const string Failed = "FAILED";
}
