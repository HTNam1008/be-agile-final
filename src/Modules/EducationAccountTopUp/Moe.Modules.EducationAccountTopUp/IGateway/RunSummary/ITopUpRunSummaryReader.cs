namespace Moe.Modules.EducationAccountTopUp.IGateway.RunSummary;

internal interface ITopUpRunSummaryReader
{
    Task<RunSummaryProjection?> GetByIdAsync(
        long runId,
        CancellationToken cancellationToken);
}

internal sealed record RunSummaryProjection(
    long RunId,
    long CampaignId,
    long OrganizationId,
    DateTime RunDateUtc,
    string TriggerType,
    string Status,
    int MatchedCount,
    int ProcessedCount,
    int SucceededCount,
    int FailedCount,
    int SkippedCount,
    decimal TotalCredited,
    DateTime? StartedAtUtc,
    DateTime? CompletedAtUtc);
