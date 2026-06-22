using Moe.Application.Abstractions.Messaging;
using Moe.Infrastructure.Shared.Api;

namespace Moe.Modules.EducationAccountTopUp.Application.TransactionHistory;

public sealed record GetAccountTransactionHistoryQuery(
    long EducationAccountId,
    int Page,
    int PageSize) : IQuery<PageResponse<AccountTransactionHistoryItem>>;

public sealed record AccountTransactionHistoryItem(
    long TransactionId,
    DateTime TransactionAtUtc,
    string TypeCode,
    string TypeLabel,
    string? Description,
    decimal Amount,
    decimal BalanceAfter,
    string PerformedBy);
