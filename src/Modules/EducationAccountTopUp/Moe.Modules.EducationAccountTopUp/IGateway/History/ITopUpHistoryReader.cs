using Moe.Modules.EducationAccountTopUp.Application.History;

namespace Moe.Modules.EducationAccountTopUp.IGateway.History;

internal interface ITopUpHistoryReader
{
    Task<HistoryPage<CampaignHistoryProjection>> GetCampaignHistoryAsync(
        TopUpHistoryFilter filter,
        TopUpAccessScope accessScope,
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    Task<HistoryPage<RunHistoryProjection>> GetRunHistoryAsync(
        TopUpHistoryFilter filter,
        TopUpAccessScope accessScope,
        int page,
        int pageSize,
        CancellationToken cancellationToken);
}

internal sealed record HistoryPage<T>(
    IReadOnlyList<T> Items,
    long TotalCount);

internal sealed record CampaignHistoryProjection(
    long CampaignId,
    string CampaignCode,
    string CampaignName,
    long OrganizationId,
    int Version,
    string ScheduleType,
    DateOnly StartDate,
    DateOnly? EndDate,
    DateTime? NextRunAtUtc,
    string Status,
    long CreatedByUserId,
    DateTime CreatedAtUtc,
    long? UpdatedByUserId,
    DateTime? UpdatedAtUtc);

internal sealed record RunHistoryProjection(
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
