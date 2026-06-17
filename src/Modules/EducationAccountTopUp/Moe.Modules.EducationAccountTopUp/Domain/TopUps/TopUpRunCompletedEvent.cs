using Moe.SharedKernel.Domain;

namespace Moe.Modules.EducationAccountTopUp.Domain.TopUps;

public sealed record TopUpRunCompletedEvent(
    long TopUpRunId,
    long CampaignId,
    string TerminalStatus,
    int TotalSucceeded,
    int TotalFailed,
    int TotalSkipped,
    decimal TotalAmount,
    DateTime OccurredAt) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; } = new DateTimeOffset(
        DateTime.SpecifyKind(OccurredAt, DateTimeKind.Utc));
}
