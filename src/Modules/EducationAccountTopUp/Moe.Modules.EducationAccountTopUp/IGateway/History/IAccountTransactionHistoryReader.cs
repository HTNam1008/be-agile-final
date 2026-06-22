namespace Moe.Modules.EducationAccountTopUp.IGateway.History;

internal interface IAccountTransactionHistoryReader
{
    Task<HistoryPage<AccountTransactionHistoryProjection>> GetTransactionsAsync(
        long educationAccountId,
        int page,
        int pageSize,
        CancellationToken cancellationToken);
}

internal sealed record AccountTransactionHistoryProjection(
    long TransactionId,
    DateTime TransactionAtUtc,
    string TransactionTypeCode,
    decimal Amount,
    string ReferenceTypeCode,
    string? Description,
    decimal BalanceAfter,
    long? CreatedByLoginAccountId);
