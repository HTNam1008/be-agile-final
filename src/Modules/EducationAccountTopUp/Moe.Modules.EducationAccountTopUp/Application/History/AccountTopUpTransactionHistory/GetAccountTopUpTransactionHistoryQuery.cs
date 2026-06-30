using Moe.Application.Abstractions.Messaging;
using Moe.Infrastructure.Shared.Api;

namespace Moe.Modules.EducationAccountTopUp.Application.History.AccountTopUpTransactionHistory;

public sealed record GetAccountTopUpTransactionHistoryQuery(
    long EducationAccountId,
    TopUpHistoryFilter Filter,
    int Page,
    int PageSize) : IQuery<PageResponse<AccountTopUpTransactionHistoryItem>>;

public sealed record AccountTopUpTransactionHistoryItem(
    long TransactionId,
    long RunId,
    string CampaignName,
    decimal Amount,
    string CurrencyCode,
    string Status,
    string? FailureReason,
    DateTime? ProcessedAtUtc,
    DateTime RunDateUtc);
