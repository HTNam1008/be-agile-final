namespace Moe.Modules.EducationAccountTopUp.Application.EducationAccounts.GetMyEducationAccount;

public sealed record MyEducationAccountDto(
    long EducationAccountId,
    long PersonId,
    string AccountNumber,
    string CurrencyCode,
    string AccountStatusCode,
    decimal CurrentBalance,
    DateTimeOffset OpenedAtUtc,
    string? OpeningTypeCode,
    string? OpeningReason,
    DateTimeOffset? PendingClosureAtUtc,
    DateTimeOffset? ClosedAtUtc,
    object Transactions);
