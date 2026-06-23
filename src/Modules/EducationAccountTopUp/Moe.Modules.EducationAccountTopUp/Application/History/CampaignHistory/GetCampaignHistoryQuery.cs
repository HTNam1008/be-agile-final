using Moe.Application.Abstractions.Messaging;
using Moe.Infrastructure.Shared.Api;

namespace Moe.Modules.EducationAccountTopUp.Application.History.CampaignHistory;

public sealed record GetCampaignHistoryQuery(
    TopUpHistoryFilter Filter,
    int Page,
    int PageSize) : IQuery<PageResponse<CampaignHistoryItem>>;

public sealed record CampaignHistoryItem(
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
