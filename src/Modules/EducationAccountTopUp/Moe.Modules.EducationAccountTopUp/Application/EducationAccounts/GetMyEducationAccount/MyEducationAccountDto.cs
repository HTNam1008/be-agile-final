namespace Moe.Modules.EducationAccountTopUp.Application.EducationAccounts.GetMyEducationAccount;

public sealed record MyEducationAccountTransactionsPage(
    IReadOnlyCollection<MyEducationAccountTransactionDto> Items,
    int Page,
    int PageSize,
    long TotalCount);

public sealed record MyEducationAccountTransactionDto(
    long AccountTransactionId,
    string TransactionTypeCode,
    string Category,
    decimal Amount,
    decimal BalanceAfter,
    DateTime TransactionAtUtc,
    string ReferenceTypeCode,
    long? ReferenceId,
    long? ReversalOfTransactionId,
    bool IsReversal,
    string? Description);

public sealed record MyEducationAccountDto(
    long EducationAccountId,
    long PersonId,
    string AccountNumber,
    string CurrencyCode,
    string AccountStatusCode,
    decimal CurrentBalance,
    decimal ReservedAmount,
    decimal AvailableBalance,
    DateTimeOffset OpenedAtUtc,
    string? OpeningTypeCode,
    string? OpeningReason,
    DateTimeOffset? PendingClosureAtUtc,
    DateTimeOffset? ClosedAtUtc,
    MyEducationAccountTransactionsPage Transactions);
