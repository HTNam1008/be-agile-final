using Moe.SharedKernel.Domain;

namespace Moe.Modules.EducationAccountTopUp.Domain.TopUps;

public sealed record ManualRunRequestedEvent(
    long TopUpRunId,
    long CampaignId,
    long RequestedByUserId,
    DateTime OccurredAt) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; } = new DateTimeOffset(
        DateTime.SpecifyKind(OccurredAt, DateTimeKind.Utc));
}

public static class TopUpErrors
{
    public static readonly Moe.SharedKernel.Results.Error Unauthorized =
        new("TopUp.Unauthorized", "Insufficient permissions to execute top-up runs.");

    public static readonly Moe.SharedKernel.Results.Error CampaignNotFound =
        new("TopUp.CampaignNotFound", "Campaign not found.");

    public static readonly Moe.SharedKernel.Results.Error CampaignNotExecutable =
        new("TopUp.CampaignNotExecutable", "Campaign is not in an executable state.");

    public static readonly Moe.SharedKernel.Results.Error ActorRequired =
        new("TopUp.ActorRequired", "An authenticated admin is required.");

    public static readonly Moe.SharedKernel.Results.Error InvalidRunTransition =
        new("TopUp.InvalidRunTransition", "The requested status transition is not valid for the current run state.");

    public static readonly Moe.SharedKernel.Results.Error RunIsTerminal =
        new("TopUp.RunIsTerminal", "This run has already reached a terminal state and cannot be modified.");

    public static readonly Moe.SharedKernel.Results.Error DuplicateScheduledOccurrence =
        new("TopUp.DuplicateScheduledOccurrence", "A run for this campaign and scheduled time already exists.");

    public static readonly Moe.SharedKernel.Results.Error ReconciliationMismatch =
        new("TopUp.ReconciliationMismatch", "Reconciliation counts do not add up: totalProcessed must equal totalSucceeded + totalFailed + totalSkipped.");
}
