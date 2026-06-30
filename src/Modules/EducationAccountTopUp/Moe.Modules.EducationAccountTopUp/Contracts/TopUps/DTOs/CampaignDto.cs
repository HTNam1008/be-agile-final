namespace Moe.Modules.EducationAccountTopUp.Contracts.TopUps.DTOs;

public class CampaignDto
{
    public long TopUpCampaignId { get; set; }
    public long OrganizationId { get; set; }
    public string CampaignCode { get; set; } = string.Empty;
    public string CampaignName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string RecipientModeCode { get; set; } = string.Empty;
    public decimal DefaultTopUpAmount { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string ScheduleTypeCode { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public string? FrequencyCode { get; set; }
    public int? FrequencyInterval { get; set; }
    public DateTime? NextRunAt { get; set; }
    public string CampaignStatusCode { get; set; } = string.Empty;
    public int CampaignVersion { get; set; }
    public string DeliveryTypeCode { get; set; } = string.Empty;
    public decimal MaxTotalAmount { get; set; }
    public int TotalStudentsToppedUp { get; set; }
}

public class CreateCampaignRequest
{
    public long OrganizationId { get; set; }
    public string CampaignCode { get; set; } = string.Empty;
    public string CampaignName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string RecipientModeCode { get; set; } = string.Empty;
    public decimal DefaultTopUpAmount { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string ScheduleTypeCode { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public string? FrequencyCode { get; set; }
    public int? FrequencyInterval { get; set; }
    public string DeliveryTypeCode { get; set; } = string.Empty;
    public decimal MaxTotalAmount { get; set; }
}

public class UpdateCampaignRequest
{
    public string? CampaignCode { get; set; }
    public string CampaignName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal DefaultTopUpAmount { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string ScheduleTypeCode { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public string? FrequencyCode { get; set; }
    public int? FrequencyInterval { get; set; }
    public int CampaignVersion { get; set; }
    public string DeliveryTypeCode { get; set; } = string.Empty;
    public decimal MaxTotalAmount { get; set; }
}
