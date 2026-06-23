using Moe.SharedKernel.Domain;

namespace Moe.Modules.FasPayment.Domain.Payments;

internal sealed class PaymentPart : Entity<long>
{
    private PaymentPart() : base(0) { }

    public long PaymentId { get; private set; }
    public int SequenceNumber { get; private set; }
    public string PaymentMethodCode { get; private set; } = string.Empty;
    public long? EducationAccountId { get; private set; }
    public long? AccountTransactionId { get; private set; }
    public decimal PartAmount { get; private set; }
    public string? ProviderCode { get; private set; }
    public string? ProviderReference { get; private set; }
    public string PartStatusCode { get; private set; } = string.Empty;
    public DateTime? AuthorizedAtUtc { get; private set; }
    public DateTime? SettledAtUtc { get; private set; }
    public string? FailureReason { get; private set; }
    public long? AccountHoldId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? CompletedAtUtc { get; private set; }

    public static PaymentPart Create(
        long paymentId,
        int sequenceNumber,
        string methodCode,
        decimal amount,
        string statusCode,
        DateTime createdAtUtc)
        => new()
        {
            PaymentId = paymentId,
            SequenceNumber = sequenceNumber,
            PaymentMethodCode = methodCode,
            PartAmount = amount,
            PartStatusCode = statusCode,
            CreatedAtUtc = createdAtUtc
        };

    public void AssignEducationAccount(long educationAccountId, long? accountHoldId)
    {
        EducationAccountId = educationAccountId;
        AccountHoldId = accountHoldId;
    }

    public void AssignAccountHold(long accountHoldId)
    {
        if (accountHoldId <= 0) throw new ArgumentOutOfRangeException(nameof(accountHoldId));
        AccountHoldId = accountHoldId;
    }

    public void AssignProvider(string providerCode, string providerReference)
    {
        ProviderCode = providerCode;
        ProviderReference = providerReference;
    }

    public void AttachToPayment(long paymentId)
    {
        if (paymentId <= 0) throw new ArgumentOutOfRangeException(nameof(paymentId));
        PaymentId = paymentId;
    }

    public void MarkCompleted(string statusCode, DateTime completedAtUtc, long? accountTransactionId = null)
    {
        PartStatusCode = statusCode;
        AccountTransactionId = accountTransactionId;
        CompletedAtUtc = completedAtUtc;
        SettledAtUtc = completedAtUtc;
    }
}

internal static class PaymentPartStatusCodes
{
    public const string Pending = "PENDING";
    public const string Reserved = "RESERVED";
    public const string Captured = "CAPTURED";
    public const string Successful = "SUCCESSFUL";
    public const string Failed = "FAILED";
    public const string Released = "RELEASED";
}
