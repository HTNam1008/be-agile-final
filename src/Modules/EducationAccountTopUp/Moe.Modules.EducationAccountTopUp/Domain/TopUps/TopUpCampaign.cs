using Moe.SharedKernel.Domain;
using Moe.SharedKernel.Results;

namespace Moe.Modules.EducationAccountTopUp.Domain.TopUps;

public sealed class TopUpCampaign : Entity<long>
{
    private TopUpCampaign() : base(0) { }

    private TopUpCampaign(
        long id,
        long organizationId,
        string campaignCode,
        string campaignName,
        decimal defaultTopUpAmount,
        string reason,
        string campaignStatusCode,
        int campaignVersion,
        long createdByLoginAccountId,
        DateTime createdAtUtc) : base(id)
    {
        OrganizationId = organizationId;
        CampaignCode = campaignCode;
        CampaignName = campaignName;
        RecipientModeCode = "ALL";
        DefaultTopUpAmount = defaultTopUpAmount;
        Reason = reason;
        ScheduleTypeCode = "MANUAL";
        StartDate = DateOnly.FromDateTime(createdAtUtc);
        CampaignStatusCode = campaignStatusCode;
        CampaignVersion = campaignVersion;
        CreatedByLoginAccountId = createdByLoginAccountId;
        CreatedAtUtc = createdAtUtc;
    }

    public long OrganizationId { get; private set; }
    public string CampaignCode { get; private set; } = string.Empty;
    public string CampaignName { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public string RecipientModeCode { get; private set; } = string.Empty;
    public decimal DefaultTopUpAmount { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public string ScheduleTypeCode { get; private set; } = string.Empty;
    public DateOnly StartDate { get; private set; }
    public DateOnly? EndDate { get; private set; }
    public string? FrequencyCode { get; private set; }
    public int? FrequencyInterval { get; private set; }
    public DateTime? NextRunAtUtc { get; private set; }
    public string CampaignStatusCode { get; private set; } = string.Empty;
    public int CampaignVersion { get; private set; }
    public long CreatedByLoginAccountId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public long? UpdatedByLoginAccountId { get; private set; }
    public DateTime? UpdatedAtUtc { get; private set; }

    public bool IsExecutable => CampaignStatusCode == TopUpCampaignStatusCodes.Active;

    public static TopUpCampaign Create(
        long organizationId,
        string campaignCode,
        string campaignName,
        string? description,
        string recipientModeCode,
        decimal defaultTopUpAmount,
        string reason,
        string scheduleTypeCode,
        DateOnly startDate,
        DateOnly? endDate,
        string? frequencyCode,
        int? frequencyInterval,
        long currentUserId,
        DateTime nowUtc)
    {
        return new TopUpCampaign
        {
            OrganizationId = organizationId,
            CampaignCode = campaignCode,
            CampaignName = campaignName,
            Description = description,
            RecipientModeCode = recipientModeCode,
            DefaultTopUpAmount = defaultTopUpAmount,
            Reason = reason,
            ScheduleTypeCode = scheduleTypeCode,
            StartDate = startDate,
            EndDate = endDate,
            FrequencyCode = frequencyCode,
            FrequencyInterval = frequencyInterval,
            CampaignStatusCode = TopUpCampaignStatusCodes.Draft,
            CampaignVersion = 1,
            CreatedByLoginAccountId = currentUserId,
            CreatedAtUtc = nowUtc,
            UpdatedByLoginAccountId = currentUserId,
            UpdatedAtUtc = nowUtc
        };
    }

    public void Update(
        string campaignName,
        string? description,
        decimal defaultTopUpAmount,
        string reason,
        string scheduleTypeCode,
        DateOnly startDate,
        DateOnly? endDate,
        string? frequencyCode,
        int? frequencyInterval,
        long currentUserId,
        DateTime nowUtc)
    {
        CampaignName = campaignName;
        Description = description;
        DefaultTopUpAmount = defaultTopUpAmount;
        Reason = reason;
        ScheduleTypeCode = scheduleTypeCode;
        StartDate = startDate;
        EndDate = endDate;
        FrequencyCode = frequencyCode;
        FrequencyInterval = frequencyInterval;
        UpdatedByLoginAccountId = currentUserId;
        UpdatedAtUtc = nowUtc;
        CampaignVersion++;
    }

    public Result ChangeStatus(string newStatusCode, long currentUserId, DateTime nowUtc, bool isSystem = false)
    {
        if (CampaignStatusCode == newStatusCode) return Result.Success();

        bool isValid = CampaignStatusCode switch
        {
            TopUpCampaignStatusCodes.Draft => newStatusCode is TopUpCampaignStatusCodes.Active or TopUpCampaignStatusCodes.Cancelled,
            TopUpCampaignStatusCodes.Active => newStatusCode is TopUpCampaignStatusCodes.Paused or TopUpCampaignStatusCodes.Cancelled || (isSystem && newStatusCode == TopUpCampaignStatusCodes.Completed),
            TopUpCampaignStatusCodes.Paused => newStatusCode is TopUpCampaignStatusCodes.Active or TopUpCampaignStatusCodes.Cancelled || (isSystem && newStatusCode == TopUpCampaignStatusCodes.Completed),
            _ => false
        };

        if (!isValid)
        {
            return Result.Failure(TopUpErrors.InvalidStatusTransition);
        }

        CampaignStatusCode = newStatusCode;
        UpdatedByLoginAccountId = currentUserId;
        UpdatedAtUtc = nowUtc;
        CampaignVersion++;
        return Result.Success();
    }

    public void SetNextRunAt(DateTime? nextRunAtUtc)
    {
        NextRunAtUtc = nextRunAtUtc;
    }
}


