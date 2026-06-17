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
}
