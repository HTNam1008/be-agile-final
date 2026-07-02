using Moe.Application.Abstractions.Messaging;
using Moe.Infrastructure.Shared.Api;

namespace Moe.Modules.EducationAccountTopUp.Application.History.AllTransactionsHistory;

public sealed record GetAllTransactionsQuery(
    TopUpHistoryFilter Filter,
    int Page,
    int PageSize,
    string? SortBy = null,
    string? SortDirection = null) : IQuery<PageResponse<AllTransactionsItem>>;

public sealed record AllTransactionsItem(
    long TransactionId,
    long RunId,
    long EducationAccountId,
    string CampaignCode,
    string CampaignName,
    string MaskedAccountNumber,
    string? MaskedStudentNumber,
    string StudentDisplayName,
    decimal Amount,
    string CurrencyCode,
    string Status,
    string? FailureReason,
    DateTime? ProcessedAtUtc,
    DateTime RunDateUtc);
