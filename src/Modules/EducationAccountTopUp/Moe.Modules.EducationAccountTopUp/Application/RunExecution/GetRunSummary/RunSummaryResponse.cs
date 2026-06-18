namespace Moe.Modules.EducationAccountTopUp.Application.RunExecution.GetRunSummary;

public sealed record RunSummaryResponse
{
    public required long TopUpRunId { get; init; }
    public required long CampaignId { get; init; }
    public required string RunStatus { get; init; }
    public required string TriggerType { get; init; }
    public required int TotalSelected { get; init; }
    public required int TotalProcessed { get; init; }
    public required int TotalSucceeded { get; init; }
    public required int TotalFailed { get; init; }
    public required int TotalSkipped { get; init; }
    public required decimal TotalAmount { get; init; }
    public required DateTime RequestedAtUtc { get; init; }
    public DateTime? StartedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
    public string? Note { get; init; }
}
