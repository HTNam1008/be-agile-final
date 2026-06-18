using System.Diagnostics.Metrics;
using Moe.Modules.EducationAccountTopUp.IGateway;

namespace Moe.Modules.EducationAccountTopUp.Infrastructure.Gateways;

internal sealed class TopUpExecutionMetrics : ITopUpExecutionMetrics, IDisposable
{
    public const string MeterName = "Moe.EducationAccountTopUp.Execution";

    private readonly Meter _meter = new(MeterName);
    private readonly Counter<long> _runStartedCounter;
    private readonly Counter<long> _runCompletedCounter;
    private readonly Counter<long> _recipientProcessedCounter;
    private readonly Counter<long> _duplicateIdempotencyCounter;
    private readonly Counter<long> _accountCreditFailureCounter;
    private readonly Counter<long> _dbConflictCounter;
    private readonly Histogram<double> _runDurationMilliseconds;
    private readonly Histogram<double> _recipientsPerSecond;

    public TopUpExecutionMetrics()
    {
        _runStartedCounter = _meter.CreateCounter<long>("topup.run.started.count");
        _runCompletedCounter = _meter.CreateCounter<long>("topup.run.completed.count");
        _recipientProcessedCounter = _meter.CreateCounter<long>("topup.recipient.processed.count");
        _duplicateIdempotencyCounter = _meter.CreateCounter<long>("topup.idempotency.duplicate.count");
        _accountCreditFailureCounter = _meter.CreateCounter<long>("topup.account_credit.failure.count");
        _dbConflictCounter = _meter.CreateCounter<long>("topup.db_conflict.count");
        _runDurationMilliseconds = _meter.CreateHistogram<double>("topup.run.duration.ms");
        _recipientsPerSecond = _meter.CreateHistogram<double>("topup.run.recipients_per_second");
    }

    public void RecordRunStarted(long topUpRunId, long campaignId, int totalSelected)
    {
        _runStartedCounter.Add(
            1,
            new KeyValuePair<string, object?>("campaign.id", campaignId),
            new KeyValuePair<string, object?>("recipient.selected.count", totalSelected));
    }

    public void RecordRunCompleted(
        long topUpRunId,
        long campaignId,
        string terminalStatus,
        int totalProcessed,
        int totalSucceeded,
        int totalFailed,
        int totalSkipped,
        TimeSpan duration)
    {
        _runCompletedCounter.Add(
            1,
            new KeyValuePair<string, object?>("campaign.id", campaignId),
            new KeyValuePair<string, object?>("terminal.status", terminalStatus));

        _runDurationMilliseconds.Record(
            duration.TotalMilliseconds,
            new KeyValuePair<string, object?>("terminal.status", terminalStatus));

        double seconds = Math.Max(duration.TotalSeconds, 0.001d);
        _recipientsPerSecond.Record(
            totalProcessed / seconds,
            new KeyValuePair<string, object?>("terminal.status", terminalStatus));
    }

    public void RecordRecipientProcessed(
        long topUpRunId,
        string status,
        bool duplicateIdempotencyHit,
        bool accountCreditFailure)
    {
        _recipientProcessedCounter.Add(
            1,
            new KeyValuePair<string, object?>("status", status));

        if (duplicateIdempotencyHit)
        {
            _duplicateIdempotencyCounter.Add(1);
        }

        if (accountCreditFailure)
        {
            _accountCreditFailureCounter.Add(1);
        }
    }

    public void RecordAccountCreditDbConflict()
    {
        _dbConflictCounter.Add(1);
    }

    public void Dispose()
    {
        _meter.Dispose();
    }
}
