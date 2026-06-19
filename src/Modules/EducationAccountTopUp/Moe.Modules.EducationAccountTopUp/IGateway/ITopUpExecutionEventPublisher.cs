namespace Moe.Modules.EducationAccountTopUp.IGateway;

public interface ITopUpExecutionEventPublisher
{
    Task PublishRunStartedAsync(
        TopUpRunStartedReport report,
        CancellationToken cancellationToken = default);

    Task PublishRunCompletedAsync(
        TopUpRunCompletedReport report,
        CancellationToken cancellationToken = default);

    Task PublishTopUpReceivedAsync(
        TopUpReceivedReport report,
        CancellationToken cancellationToken = default);
}

public sealed record TopUpRunStartedReport
{
    public required long TopUpRunId { get; init; }
    public required long CampaignId { get; init; }
    public required int TotalSelected { get; init; }
    public required DateTime OccurredAtUtc { get; init; }
}

public sealed record TopUpRunCompletedReport
{
    public required long TopUpRunId { get; init; }
    public required long CampaignId { get; init; }
    public required string TerminalStatus { get; init; }
    public required int TotalProcessed { get; init; }
    public required int TotalSucceeded { get; init; }
    public required int TotalFailed { get; init; }
    public required int TotalSkipped { get; init; }
    public required decimal TotalAmount { get; init; }
    public required DateTime OccurredAtUtc { get; init; }
}

public sealed record TopUpReceivedReport
{
    public required long TopUpRunId { get; init; }
    public required long TopUpTransactionId { get; init; }
    public required long EducationAccountId { get; init; }
    public required long AccountTransactionId { get; init; }
    public required decimal Amount { get; init; }
    public required bool AlreadyProcessed { get; init; }
    public required DateTime OccurredAtUtc { get; init; }
}
