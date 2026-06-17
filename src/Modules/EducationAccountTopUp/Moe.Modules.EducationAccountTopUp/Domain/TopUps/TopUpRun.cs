using Moe.SharedKernel.Domain;

namespace Moe.Modules.EducationAccountTopUp.Domain.TopUps;

public sealed class TopUpRun : AggregateRoot<long>
{
    private TopUpRun() : base(0) { }

    private TopUpRun(
        long id,
        long topUpCampaignId,
        int campaignVersion,
        DateTime requestedAtUtc,
        string triggerTypeCode,
        long? triggeredByLoginAccountId,
        string runStatusCode,
        string idempotencyKey,
        string? note) : base(id)
    {
        TopUpCampaignId = topUpCampaignId;
        CampaignVersion = campaignVersion;
        ScheduledForUtc = requestedAtUtc;
        TriggerTypeCode = triggerTypeCode;
        TriggeredByLoginAccountId = triggeredByLoginAccountId;
        RunStatusCode = runStatusCode;
        IdempotencyKey = idempotencyKey;
        Note = note;
    }

    public long TopUpCampaignId { get; private set; }
    public int CampaignVersion { get; private set; }
    public DateTime ScheduledForUtc { get; private set; }
    public string TriggerTypeCode { get; private set; } = string.Empty;
    public long? TriggeredByLoginAccountId { get; private set; }
    public string RunStatusCode { get; private set; } = string.Empty;
    public string? RuleSnapshotJson { get; private set; }
    public int TotalSelected { get; private set; }
    public int TotalProcessed { get; private set; }
    public int TotalSucceeded { get; private set; }
    public int TotalFailed { get; private set; }
    public decimal TotalAmount { get; private set; }
    public DateTime? StartedAtUtc { get; private set; }
    public DateTime? CompletedAtUtc { get; private set; }
    public string IdempotencyKey { get; private set; } = string.Empty;
    public string? Note { get; private set; }

    public static TopUpRun CreateManual(
        TopUpCampaign campaign,
        string idempotencyKey,
        long requestedByUserId,
        DateTime requestedAtUtc,
        string? note)
    {
        return new TopUpRun(
            id: 0,
            campaign.Id,
            campaign.CampaignVersion,
            requestedAtUtc,
            TopUpRunTriggerTypes.Manual,
            requestedByUserId,
            TopUpRunStatusCodes.Previewed,
            idempotencyKey.Trim(),
            string.IsNullOrWhiteSpace(note) ? null : note.Trim());
    }

    public static TopUpRun Rehydrate(
        long id,
        long campaignId,
        int campaignVersion,
        DateTime requestedAtUtc,
        string triggerTypeCode,
        long? requestedByUserId,
        string runStatusCode,
        string idempotencyKey,
        string? note = null)
    {
        return new TopUpRun(
            id,
            campaignId,
            campaignVersion,
            requestedAtUtc,
            triggerTypeCode,
            requestedByUserId,
            runStatusCode,
            idempotencyKey,
            note);
    }

    public void MarkManualRunRequested(DateTime occurredAtUtc)
    {
        if (TriggeredByLoginAccountId is long requestedByUserId)
        {
            Raise(new ManualRunRequestedEvent(Id, TopUpCampaignId, requestedByUserId, occurredAtUtc));
        }
    }
}

public static class TopUpRunStatusCodes
{
    public const string Previewed = "PREVIEWED";
    public const string Processing = "PROCESSING";
    public const string Completed = "COMPLETED";
    public const string Partial = "PARTIAL";
    public const string Failed = "FAILED";
    public const string Cancelled = "CANCELLED";
}

public static class TopUpRunTriggerTypes
{
    public const string Manual = "MANUAL";
}
