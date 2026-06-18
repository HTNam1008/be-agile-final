using Moe.Application.Abstractions.Messaging;
using Moe.Infrastructure.Shared.Api;

namespace Moe.Modules.EducationAccountTopUp.Application.History.RunHistory;

public sealed record GetRunHistoryQuery(
    TopUpHistoryFilter Filter,
    int Page,
    int PageSize) : IQuery<PageResponse<RunHistoryItem>>;

public sealed record RunHistoryItem(
    long RunId,
    long CampaignId,
    string CampaignCode,
    string CampaignName,
    long OrganizationId,
    DateTime RunDateUtc,
    string TriggerType,
    string Status,
    int MatchedCount,
    int ProcessedCount,
    int SucceededCount,
    int FailedCount,
    decimal TotalCredited,
    long? TriggeredByUserId,
    DateTime? StartedAtUtc,
    DateTime? CompletedAtUtc);
