using Moe.Application.Abstractions.Messaging;
using Moe.Infrastructure.Shared.Api;

namespace Moe.Modules.EducationAccountTopUp.Application.History.CampaignTransactionHistory;

public sealed record GetCampaignTransactionHistoryQuery(
    long CampaignId,
    TopUpHistoryFilter Filter,
    int Page,
    int PageSize) : IQuery<PageResponse<CampaignTransactionHistoryItem>>;

public sealed record CampaignTransactionHistoryItem(
    long TransactionId,
    long RunId,
    long EducationAccountId,
    string MaskedAccountNumber,
    string? MaskedStudentNumber,
    string StudentDisplayName,
    decimal Amount,
    string CurrencyCode,
    string Status,
    string? FailureReason,
    DateTime? ProcessedAtUtc,
    DateTime RunDateUtc);
