using Moe.SharedKernel.Domain;
using Moe.SharedKernel.Results;

namespace Moe.Modules.EducationAccountTopUp.Domain.TopUps;

public sealed class TopUpRun : AggregateRoot<long>
{
    private TopUpRun() : base(0) { }

    public DateTime CreatedAtUtc { get; private set; }

    private TopUpRun(
        long id,
        long topUpCampaignId,
        int campaignVersion,
        DateTime requestedAtUtc,
        string triggerTypeCode,
        long? triggeredByLoginAccountId,
        string runStatusCode,
        string idempotencyKey,
        string? note,
        string? ruleSnapshotJson = null) : base(id)
    {
        TopUpCampaignId = topUpCampaignId;
        CampaignVersion = campaignVersion;
        ScheduledForUtc = requestedAtUtc;
        TriggerTypeCode = triggerTypeCode;
        TriggeredByLoginAccountId = triggeredByLoginAccountId;
        RunStatusCode = runStatusCode;
        IdempotencyKey = idempotencyKey;
        Note = note;
        RuleSnapshotJson = ruleSnapshotJson;
    }

    public long TopUpCampaignId { get; private set; }
    public int CampaignVersion { get; private set; }
    public DateTime ScheduledForUtc { get; private set; }
    public string TriggerTypeCode { get; private set; } = string.Empty;
    public long? TriggeredByLoginAccountId { get; private set; }
    public string RunStatusCode { get; private set; } = string.Empty;
    public string? RuleSnapshotJson { get; private set; }
    public bool IsContractDriven { get; private set; }
    public string? RunTypeCode { get; private set; }
    public int TotalSelected { get; private set; }
    public int TotalProcessed { get; private set; }
    public int TotalSucceeded { get; private set; }
    public int TotalFailed { get; private set; }
    public int TotalSkipped { get; private set; }
    public decimal TotalAmount { get; private set; }
    public DateTime? StartedAtUtc { get; private set; }
    public DateTime? CompletedAtUtc { get; private set; }
    public DateTime? CancelRequestedAtUtc { get; private set; }
    public string IdempotencyKey { get; private set; } = string.Empty;
    public string? Note { get; private set; }

    public DateTime ScheduledFor => ScheduledForUtc;

    public DateTime? StartedAt => StartedAtUtc;

    public DateTime? CompletedAt => CompletedAtUtc;

    private bool IsTerminal => TopUpRunStatusCodes.TerminalStatuses.Contains(RunStatusCode);

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

    public static TopUpRun CreateScheduled(
        long campaignId,
        int campaignVersion,
        DateTime scheduledFor,
        string idempotencyKey,
        string? ruleSnapshotJson,
        DateTime utcNow)
    {
        _ = utcNow;

        return new TopUpRun(
            id: 0,
            campaignId,
            campaignVersion,
            scheduledFor,
            TopUpRunTriggerTypes.Scheduled,
            triggeredByLoginAccountId: null,
            TopUpRunStatusCodes.Previewed,
            idempotencyKey.Trim(),
            note: null,
            string.IsNullOrWhiteSpace(ruleSnapshotJson) ? null : ruleSnapshotJson);
    }

    public static TopUpRun CreateForContracts(long campaignId, int campaignVersion, DateTime utcNow)
    {
        return new TopUpRun
        {
            TopUpCampaignId = campaignId,
            CampaignVersion = campaignVersion,
            ScheduledForUtc = utcNow,
            TriggerTypeCode = TopUpRunTriggerTypes.Scheduled,
            RunStatusCode = TopUpRunStatusCodes.Previewed,
            IdempotencyKey = $"contract-run-{campaignId}-{utcNow:yyyyMMddHHmmss}",
            IsContractDriven = true,
            RunTypeCode = "CONTRACT",
            CreatedAtUtc = utcNow,
        };
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

    public Result StartProcessing(DateTime utcNow)
    {
        Result transition = ValidateTransition(TopUpRunStatusCodes.Previewed, TopUpRunStatusCodes.Processing);
        if (transition.IsFailure)
        {
            return transition;
        }

        RunStatusCode = TopUpRunStatusCodes.Processing;
        StartedAtUtc = utcNow;
        Raise(new TopUpRunStartedEvent(Id, TopUpCampaignId, utcNow));
        return Result.Success();
    }

    public Result Cancel(DateTime utcNow)
    {
        Result transition = ValidateTransition(RunStatusCode, TopUpRunStatusCodes.Cancelled);
        if (transition.IsFailure)
        {
            return transition;
        }

        RunStatusCode = TopUpRunStatusCodes.Cancelled;
        CompletedAtUtc = utcNow;
        Raise(new TopUpRunCancelledEvent(Id, TopUpCampaignId, utcNow));
        return Result.Success();
    }

    public Result Finalize(
        int totalProcessed,
        int totalSucceeded,
        int totalFailed,
        int totalSkipped,
        decimal totalAmount,
        DateTime utcNow)
    {
        if (totalProcessed != totalSucceeded + totalFailed + totalSkipped)
        {
            return Result.Failure(TopUpErrors.ReconciliationMismatch);
        }

        string terminalStatus = DetermineTerminalStatus(totalSucceeded, totalFailed, totalSkipped);
        Result transition = ValidateTransition(TopUpRunStatusCodes.Processing, terminalStatus);
        if (transition.IsFailure)
        {
            return transition;
        }

        TotalProcessed = totalProcessed;
        TotalSucceeded = totalSucceeded;
        TotalFailed = totalFailed;
        TotalSkipped = totalSkipped;
        TotalAmount = totalAmount;
        RunStatusCode = terminalStatus;
        CompletedAtUtc = utcNow;

        Raise(new TopUpRunCompletedEvent(
            Id,
            TopUpCampaignId,
            terminalStatus,
            totalSucceeded,
            totalFailed,
            totalSkipped,
            totalAmount,
            utcNow));

        return Result.Success();
    }

    public Result SetTotalSelected(int count)
    {
        if (IsTerminal)
        {
            return Result.Failure(TopUpErrors.RunIsTerminal);
        }

        if (RunStatusCode is not (TopUpRunStatusCodes.Previewed or TopUpRunStatusCodes.Processing))
        {
            return Result.Failure(TopUpErrors.InvalidRunTransition);
        }

        TotalSelected = count;
        return Result.Success();
    }

    public Result CaptureRuleSnapshot(string ruleJson)
    {
        if (IsTerminal)
        {
            return Result.Failure(TopUpErrors.RunIsTerminal);
        }

        if (RunStatusCode != TopUpRunStatusCodes.Previewed)
        {
            return Result.Failure(TopUpErrors.InvalidRunTransition);
        }

        RuleSnapshotJson = ruleJson;
        return Result.Success();
    }

    public void ReconcileCounters(int totalProcessed, int totalSucceeded, int totalFailed, int totalSkipped, decimal totalAmount)
    {
        TotalProcessed = totalProcessed;
        TotalSucceeded = totalSucceeded;
        TotalFailed = totalFailed;
        TotalSkipped = totalSkipped;
        TotalAmount = totalAmount;
    }

    public void MarkManualRunRequested(DateTime occurredAtUtc)
    {
        if (TriggeredByLoginAccountId is long requestedByUserId)
        {
            Raise(new ManualRunRequestedEvent(Id, TopUpCampaignId, requestedByUserId, occurredAtUtc));
        }
    }

    public void RequestCancel(DateTime utcNow)
    {
        if (IsTerminal || CancelRequestedAtUtc.HasValue)
        {
            return;
        }

        CancelRequestedAtUtc = utcNow;
    }

    public bool IsCancelRequested => CancelRequestedAtUtc.HasValue;

    private Result ValidateTransition(string from, string to)
    {
        if (IsTerminal)
        {
            return Result.Failure(TopUpErrors.RunIsTerminal);
        }

        if (RunStatusCode != from)
        {
            return Result.Failure(TopUpErrors.InvalidRunTransition);
        }

        return TopUpRunStatusCodes.ValidTransitions.TryGetValue(from, out IReadOnlySet<string>? targets)
            && targets.Contains(to)
            ? Result.Success()
            : Result.Failure(TopUpErrors.InvalidRunTransition);
    }

    private static string DetermineTerminalStatus(int totalSucceeded, int totalFailed, int totalSkipped)
    {
        if (totalSucceeded == 0)
        {
            return TopUpRunStatusCodes.Failed;
        }

        if (totalFailed == 0 && totalSkipped == 0)
        {
            return TopUpRunStatusCodes.Completed;
        }

        return TopUpRunStatusCodes.Partial;
    }
}
