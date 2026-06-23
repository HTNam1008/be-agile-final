namespace Moe.Modules.EducationAccountTopUp.IGateway;

public interface ITopUpExecutionMetrics
{
    void RecordRunStarted(long topUpRunId, long campaignId, int totalSelected);

    void RecordRunCompleted(
        long topUpRunId,
        long campaignId,
        string terminalStatus,
        int totalProcessed,
        int totalSucceeded,
        int totalFailed,
        int totalSkipped,
        TimeSpan duration);

    void RecordRecipientProcessed(
        long topUpRunId,
        string status,
        bool duplicateIdempotencyHit,
        bool accountCreditFailure);

    void RecordAccountCreditDbConflict();
}
