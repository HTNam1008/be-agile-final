using Moe.SharedKernel.Domain;
using Moe.SharedKernel.Results;

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
    public static readonly Error Unauthorized =
        new("TopUp.Unauthorized", "Insufficient permissions to execute top-up runs.");

    public static readonly Error CampaignNotFound =
        new("TopUp.CampaignNotFound", "Campaign not found.");

    public static readonly Error CampaignNotExecutable =
        new("TopUp.CampaignNotExecutable", "Campaign is not in an executable state.");

    public static readonly Error ActorRequired =
        new("TopUp.ActorRequired", "An authenticated admin is required.");

    public static readonly Error ReconciliationMismatch = new(
    "TopUp.ReconciliationMismatch",
    "Reconciliation counts do not add up: totalProcessed must equal totalSucceeded + totalFailed + totalSkipped.");

    public static readonly Error RunIsTerminal = new(
        "TopUp.RunIsTerminal",
        "This run has already reached a terminal state and cannot be modified.");

    public static readonly Error InvalidRunTransition = new(
        "TopUp.InvalidRunTransition",
        "The requested status transition is not valid for the current run state.");
}
