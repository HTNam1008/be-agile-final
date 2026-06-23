using Moe.SharedKernel.Domain;

namespace Moe.Modules.EducationAccountTopUp.Domain.EducationAccounts;

internal sealed class AccountHold : Entity<long>
{
    private AccountHold() : base(0) { }

    public long EducationAccountId { get; private set; }
    public long? PaymentPartId { get; private set; }
    public decimal HoldAmount { get; private set; }
    public string HoldStatusCode { get; private set; } = string.Empty;
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime ExpiresAtUtc { get; private set; }
    public DateTime? ConvertedAtUtc { get; private set; }
    public long? AccountTransactionId { get; private set; }

    public static AccountHold Reserve(
        long educationAccountId,
        long paymentPartId,
        decimal amount,
        DateTime createdAtUtc,
        DateTime expiresAtUtc)
        => educationAccountId <= 0 || paymentPartId <= 0 || amount <= 0m || expiresAtUtc <= createdAtUtc
            ? throw new ArgumentOutOfRangeException(nameof(amount))
            : new AccountHold
            {
                EducationAccountId = educationAccountId,
                PaymentPartId = paymentPartId,
                HoldAmount = amount,
                HoldStatusCode = AccountHoldStatusCodes.Reserved,
                CreatedAtUtc = createdAtUtc,
                ExpiresAtUtc = expiresAtUtc
            };

    public void Capture(long accountTransactionId, DateTime convertedAtUtc)
    {
        if (HoldStatusCode != AccountHoldStatusCodes.Reserved)
            throw new InvalidOperationException("Only a reserved account hold can be captured.");
        AccountTransactionId = accountTransactionId;
        HoldStatusCode = AccountHoldStatusCodes.Captured;
        ConvertedAtUtc = convertedAtUtc;
    }

    public void Release(DateTime convertedAtUtc)
    {
        if (HoldStatusCode != AccountHoldStatusCodes.Reserved) return;
        HoldStatusCode = AccountHoldStatusCodes.Released;
        ConvertedAtUtc = convertedAtUtc;
    }
}

internal static class AccountHoldStatusCodes
{
    public const string Reserved = "RESERVED";
    public const string Captured = "CAPTURED";
    public const string Released = "RELEASED";
}
