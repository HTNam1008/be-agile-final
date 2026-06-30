using Moe.Application.Abstractions.Messaging;

namespace Moe.Modules.EducationAccountTopUp.Application.TopUps.GetCampaigns;

public sealed record GetCampaignsQuery(
    int PageNumber = 1,
    int PageSize = 50,
    string? Search = null,
    string? Status = null,
    string? DateFrom = null,
    string? DateTo = null) : IQuery<CampaignListResult>;

public sealed record CampaignListResult(
    IReadOnlyList<CampaignListItem> Items,
    int TotalCount,
    int PageNumber,
    int PageSize);

public sealed record CampaignListItem(
    long Id,
    long OrganizationId,
    string CampaignCode,
    string CampaignName,
    string? Description,
    string RecipientModeCode,
    decimal DefaultTopUpAmount,
    string Reason,
    string ScheduleTypeCode,
    DateOnly StartDate,
    DateOnly? EndDate,
    string? FrequencyCode,
    int? FrequencyInterval,
    DateTime? NextRunAt,
    string CampaignStatusCode,
    int CampaignVersion,
    string DeliveryTypeCode,
    decimal MaxTotalAmount,
    long CreatedByLoginAccountId,
    long? UpdatedByLoginAccountId,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);
