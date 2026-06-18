using Moe.Modules.EducationAccountTopUp.Domain.TopUps;

namespace Moe.Modules.EducationAccountTopUp.Application.RunExecution;

public sealed record ReconciliationResult
{
    public required long TopUpRunId { get; init; }
    public required string RunStatus { get; init; }
    public required string ReconciliationStatus { get; init; }
    public required TransactionSummary Summary { get; init; }
    public string? MismatchDetails { get; init; }

    public static ReconciliationResult Finalized(long runId, string runStatus, TransactionSummary summary)
    {
        return new ReconciliationResult
        {
            TopUpRunId = runId,
            RunStatus = runStatus,
            ReconciliationStatus = "Finalized",
            Summary = summary
        };
    }

    public static ReconciliationResult Verified(long runId, string runStatus, TransactionSummary summary)
    {
        return new ReconciliationResult
        {
            TopUpRunId = runId,
            RunStatus = runStatus,
            ReconciliationStatus = "Verified",
            Summary = summary
        };
    }

    public static ReconciliationResult Mismatch(
        long runId,
        string runStatus,
        TransactionSummary summary,
        string details)
    {
        return new ReconciliationResult
        {
            TopUpRunId = runId,
            RunStatus = runStatus,
            ReconciliationStatus = "Mismatch",
            Summary = summary,
            MismatchDetails = details
        };
    }

    public static ReconciliationResult Incomplete(long runId, TransactionSummary summary)
    {
        return new ReconciliationResult
        {
            TopUpRunId = runId,
            RunStatus = TopUpRunStatusCodes.Processing,
            ReconciliationStatus = "Incomplete",
            Summary = summary
        };
    }
}
