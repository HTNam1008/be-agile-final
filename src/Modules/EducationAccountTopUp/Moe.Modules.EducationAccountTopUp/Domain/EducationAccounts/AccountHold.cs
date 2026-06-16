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
}
