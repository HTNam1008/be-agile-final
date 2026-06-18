namespace Moe.Modules.EducationAccountTopUp.Application.RunExecution.GetRunSummary;

public sealed record RunSummaryResponse
{
    public required long RunId { get; init; }
    public required long CampaignId { get; init; }
    public required DateTime RunDateUtc { get; init; }
    public required string TriggerType { get; init; }
    public required string Status { get; init; }
    public required int MatchedCount { get; init; }
    public required int ProcessedCount { get; init; }
    public required int SucceededCount { get; init; }
    public required int FailedCount { get; init; }
    public required int SkippedCount { get; init; }
    public required decimal TotalCredited { get; init; }
    public DateTime? StartedAtUtc { get; init; }
    public DateTime? CompletedAtUtc { get; init; }
}
