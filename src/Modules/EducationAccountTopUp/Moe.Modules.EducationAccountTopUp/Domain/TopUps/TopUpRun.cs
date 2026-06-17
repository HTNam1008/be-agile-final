using Moe.SharedKernel.Domain;

namespace Moe.Modules.EducationAccountTopUp.Domain.TopUps;

internal sealed class TopUpRun : Entity<long>
{
    private TopUpRun() : base(0) { }

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

    public static TopUpRun Create(
        long topUpCampaignId,
        int campaignVersion,
        DateTime scheduledForUtc,
        string triggerTypeCode,
        long? triggeredByLoginAccountId,
        string ruleSnapshotJson,
        string idempotencyKey,
        DateTime nowUtc)
    {
        return new TopUpRun
        {
            TopUpCampaignId = topUpCampaignId,
            CampaignVersion = campaignVersion,
            ScheduledForUtc = scheduledForUtc,
            TriggerTypeCode = triggerTypeCode,
            TriggeredByLoginAccountId = triggeredByLoginAccountId,
            RunStatusCode = "PROCESSING",
            RuleSnapshotJson = ruleSnapshotJson,
            IdempotencyKey = idempotencyKey,
            StartedAtUtc = nowUtc,
            TotalSelected = 0,
            TotalProcessed = 0,
            TotalSucceeded = 0,
            TotalFailed = 0,
            TotalAmount = 0
        };
    }

    public void UpdateProgress(int succeededCount, int failedCount, decimal amount, DateTime nowUtc)
    {
        TotalSucceeded += succeededCount;
        TotalFailed += failedCount;
        TotalProcessed = TotalSucceeded + TotalFailed;
        TotalAmount += amount;
    }

    public void Complete(string finalStatusCode, DateTime nowUtc, int totalSelected)
    {
        TotalSelected = totalSelected;
        RunStatusCode = finalStatusCode;
        CompletedAtUtc = nowUtc;
    }
}
