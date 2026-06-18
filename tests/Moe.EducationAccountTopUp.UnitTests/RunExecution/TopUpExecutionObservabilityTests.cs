using System.Diagnostics.Metrics;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moe.Modules.EducationAccountTopUp.IGateway;
using Moe.Modules.EducationAccountTopUp.Infrastructure.Gateways;
using Xunit;

namespace Moe.EducationAccountTopUp.UnitTests.RunExecution;

public sealed class TopUpExecutionObservabilityTests
{
    [Fact]
    public void Should_Record_Execution_Metrics()
    {
        List<string> measurements = [];

        using MeterListener listener = new();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == TopUpExecutionMetrics.MeterName)
            {
                meterListener.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>(
            (instrument, measurement, tags, state) => measurements.Add(instrument.Name));
        listener.SetMeasurementEventCallback<double>(
            (instrument, measurement, tags, state) => measurements.Add(instrument.Name));
        listener.Start();

        using TopUpExecutionMetrics metrics = new();
        metrics.RecordRunStarted(1, 10, 2);
        metrics.RecordRecipientProcessed(1, "COMPLETED", duplicateIdempotencyHit: true, accountCreditFailure: false);
        metrics.RecordRecipientProcessed(1, "FAILED", duplicateIdempotencyHit: false, accountCreditFailure: true);
        metrics.RecordAccountCreditDbConflict();
        metrics.RecordRunCompleted(1, 10, "PARTIAL", 2, 1, 1, 0, TimeSpan.FromSeconds(2));

        measurements.Should().Contain("topup.run.started.count");
        measurements.Should().Contain("topup.recipient.processed.count");
        measurements.Should().Contain("topup.idempotency.duplicate.count");
        measurements.Should().Contain("topup.account_credit.failure.count");
        measurements.Should().Contain("topup.db_conflict.count");
        measurements.Should().Contain("topup.run.duration.ms");
        measurements.Should().Contain("topup.run.recipients_per_second");
        measurements.Should().Contain("topup.run.completed.count");
    }

    [Fact]
    public async Task Should_Not_Log_Sensitive_Payloads_For_Execution_Events()
    {
        TestLogger<LoggingTopUpExecutionEventPublisher> logger = new();
        LoggingTopUpExecutionEventPublisher publisher = new(logger);

        await publisher.PublishRunStartedAsync(new TopUpRunStartedReport
        {
            TopUpRunId = 1,
            CampaignId = 10,
            TotalSelected = 1,
            OccurredAtUtc = DateTime.UtcNow
        });

        await publisher.PublishTopUpReceivedAsync(new TopUpReceivedReport
        {
            TopUpRunId = 1,
            TopUpTransactionId = 2,
            EducationAccountId = 3,
            AccountTransactionId = 4,
            Amount = 100m,
            AlreadyProcessed = false,
            OccurredAtUtc = DateTime.UtcNow
        });

        await publisher.PublishRunCompletedAsync(new TopUpRunCompletedReport
        {
            TopUpRunId = 1,
            CampaignId = 10,
            TerminalStatus = "COMPLETED",
            TotalProcessed = 1,
            TotalSucceeded = 1,
            TotalFailed = 0,
            TotalSkipped = 0,
            TotalAmount = 100m,
            OccurredAtUtc = DateTime.UtcNow
        });

        string logText = string.Join(Environment.NewLine, logger.Messages);
        logText.Should().NotContain("NRIC");
        logText.Should().NotContain("IdentityNumber");
        logText.Should().NotContain("OfficialFullName");
        logText.Should().NotContain("Authorization");
        logText.Should().NotContain("Bearer");
        logText.ToLowerInvariant().Should().NotContain("token");
        logText.ToLowerInvariant().Should().NotContain("secret");
    }

    private sealed class TestLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
            => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Messages.Add(formatter(state, exception));
        }
    }
}
