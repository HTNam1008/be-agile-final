using Moe.Application.Abstractions.Messaging;

namespace Moe.Modules.EducationAccountTopUp.Application.TopUps.GetCampaigns;

public sealed record GetCampaignsQuery : IQuery<IReadOnlyList<CampaignListItem>>;

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
    DateTime CreatedAt,
    DateTime? UpdatedAt);
