using Moe.SharedKernel.Domain;

namespace Moe.Modules.EducationAccountTopUp.Domain.TopUps;

public sealed record TopUpRunStartedEvent(
    long TopUpRunId,
    long CampaignId,
    DateTime OccurredAt) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; } = new DateTimeOffset(
        DateTime.SpecifyKind(OccurredAt, DateTimeKind.Utc));
}
