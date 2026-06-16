using Moe.SharedKernel.Domain;

namespace Moe.Modules.EducationAccountTopUp.Domain.EducationAccounts;

internal sealed class AccountTransaction : Entity<long>
{
    private AccountTransaction() : base(0) { }

    public long EducationAccountId { get; private set; }
    public string TransactionTypeCode { get; private set; } = string.Empty;
    public decimal Amount { get; private set; }
    public DateTime TransactionAtUtc { get; private set; }
    public string ReferenceTypeCode { get; private set; } = string.Empty;
    public long? ReferenceId { get; private set; }
    public string IdempotencyKey { get; private set; } = string.Empty;
    public long? ReversalOfTransactionId { get; private set; }
    public decimal BalanceAfter { get; private set; }
    public string? Description { get; private set; }
    public long? CreatedByLoginAccountId { get; private set; }
}
