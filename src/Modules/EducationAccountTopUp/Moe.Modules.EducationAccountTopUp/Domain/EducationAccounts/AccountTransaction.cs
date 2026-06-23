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

    public static AccountTransaction Create(
        long educationAccountId,
        string transactionTypeCode,
        decimal amount,
        string referenceTypeCode,
        long? referenceId,
        string idempotencyKey,
        decimal currentBalance,
        string description,
        long? createdByUserId,
        DateTime nowUtc,
        long? reversalOfTransactionId = null)
    {
        return new AccountTransaction
        {
            EducationAccountId = educationAccountId,
            TransactionTypeCode = transactionTypeCode,
            Amount = amount,
            TransactionAtUtc = nowUtc,
            ReferenceTypeCode = referenceTypeCode,
            ReferenceId = referenceId,
            IdempotencyKey = idempotencyKey,
            BalanceAfter = currentBalance + amount,
            ReversalOfTransactionId = reversalOfTransactionId,
            Description = description,
            CreatedByLoginAccountId = createdByUserId
        };
    }
}
