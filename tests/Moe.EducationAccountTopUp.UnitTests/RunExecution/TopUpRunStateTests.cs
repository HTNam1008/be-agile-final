using FluentAssertions;
using Moe.Modules.EducationAccountTopUp.Domain.TopUps;
using Xunit;

namespace Moe.EducationAccountTopUp.UnitTests.RunExecution;

public sealed class TopUpRunStateTests
{
    private readonly DateTime _utcNow = new(2026, 6, 17, 4, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Should_Transition_From_Previewed_To_Processing()
    {
        TopUpRun run = CreateRun();

        var result = run.StartProcessing(_utcNow);

        result.IsSuccess.Should().BeTrue();
        run.RunStatusCode.Should().Be(TopUpRunStatusCodes.Processing);
        run.StartedAtUtc.Should().Be(_utcNow);
        run.DomainEvents.OfType<TopUpRunStartedEvent>().Should().ContainSingle();
    }

    [Fact]
    public void Should_Transition_From_Processing_To_Completed()
    {
        TopUpRun run = CreateProcessingRun();

        var result = run.Finalize(3, 3, 0, 0, 150m, _utcNow);

        result.IsSuccess.Should().BeTrue();
        run.RunStatusCode.Should().Be(TopUpRunStatusCodes.Completed);
        run.CompletedAtUtc.Should().Be(_utcNow);
        run.TotalProcessed.Should().Be(3);
        run.TotalSucceeded.Should().Be(3);
        run.TotalFailed.Should().Be(0);
        run.TotalSkipped.Should().Be(0);
        run.TotalAmount.Should().Be(150m);
        run.DomainEvents.OfType<TopUpRunCompletedEvent>()
            .Should()
            .ContainSingle()
            .Which
            .TerminalStatus
            .Should()
            .Be(TopUpRunStatusCodes.Completed);
    }

    [Fact]
    public void Should_Transition_From_Processing_To_Partial()
    {
        TopUpRun run = CreateProcessingRun();

        var result = run.Finalize(3, 1, 1, 1, 50m, _utcNow);

        result.IsSuccess.Should().BeTrue();
        run.RunStatusCode.Should().Be(TopUpRunStatusCodes.Partial);
    }

    [Fact]
    public void Should_Transition_From_Processing_To_Failed()
    {
        TopUpRun run = CreateProcessingRun();

        var result = run.Finalize(3, 0, 2, 1, 0m, _utcNow);

        result.IsSuccess.Should().BeTrue();
        run.RunStatusCode.Should().Be(TopUpRunStatusCodes.Failed);
    }

    [Fact]
    public void Should_Transition_From_Previewed_To_Cancelled()
    {
        TopUpRun run = CreateRun();

        var result = run.Cancel(_utcNow);

        result.IsSuccess.Should().BeTrue();
        run.RunStatusCode.Should().Be(TopUpRunStatusCodes.Cancelled);
        run.CompletedAtUtc.Should().Be(_utcNow);
        run.DomainEvents.OfType<TopUpRunCancelledEvent>().Should().ContainSingle();
    }

    [Fact]
    public void Should_Reject_Transition_From_Terminal_State()
    {
        TopUpRun run = CreateProcessingRun();
        run.Finalize(1, 1, 0, 0, 25m, _utcNow).IsSuccess.Should().BeTrue();

        var result = run.StartProcessing(_utcNow.AddMinutes(1));

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(TopUpErrors.RunIsTerminal);
    }

    [Fact]
    public void Should_Reject_Invalid_Transition()
    {
        TopUpRun run = CreateProcessingRun();

        var result = run.Cancel(_utcNow);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(TopUpErrors.InvalidRunTransition);
    }

    [Fact]
    public void Should_Reject_Finalize_When_Counts_Mismatch()
    {
        TopUpRun run = CreateProcessingRun();

        var result = run.Finalize(3, 1, 1, 0, 50m, _utcNow);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(TopUpErrors.ReconciliationMismatch);
        run.RunStatusCode.Should().Be(TopUpRunStatusCodes.Processing);
    }

    [Fact]
    public void Should_Persist_Rule_Snapshot_Json()
    {
        TopUpRun run = CreateRun();
        const string snapshot = "{\"rules\":[{\"type\":\"student\"}]}";

        var result = run.CaptureRuleSnapshot(snapshot);

        result.IsSuccess.Should().BeTrue();
        run.RuleSnapshotJson.Should().Be(snapshot);
    }

    [Fact]
    public void Should_Reject_Rule_Snapshot_After_Processing_Starts()
    {
        TopUpRun run = CreateProcessingRun();

        var result = run.CaptureRuleSnapshot("{\"rules\":[]}");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(TopUpErrors.InvalidRunTransition);
    }

    [Fact]
    public void Should_Prevent_Direct_Modification_Of_Reconciliation_Fields()
    {
        string[] propertyNames =
        [
            nameof(TopUpRun.RunStatusCode),
            nameof(TopUpRun.TotalProcessed),
            nameof(TopUpRun.TotalSucceeded),
            nameof(TopUpRun.TotalFailed),
            nameof(TopUpRun.TotalSkipped),
            nameof(TopUpRun.TotalAmount)
        ];

        foreach (string propertyName in propertyNames)
        {
            typeof(TopUpRun)
                .GetProperty(propertyName)!
                .SetMethod!
                .IsPublic
                .Should()
                .BeFalse($"{propertyName} must not have a public setter");
        }
    }

    private TopUpRun CreateProcessingRun()
    {
        TopUpRun run = CreateRun();
        run.StartProcessing(_utcNow).IsSuccess.Should().BeTrue();
        run.ClearDomainEvents();
        return run;
    }

    private static TopUpRun CreateRun()
    {
        var campaign = TopUpCampaign.Create(1, "CAMPAIGN-01", "Test", null, "FIXED", 100m, "Reason", "IMMEDIATE", new DateOnly(2026, 1, 1), null, null, null, "INSTANT", 100m, 99, DateTime.UtcNow);
        typeof(Moe.SharedKernel.Domain.Entity<long>).GetProperty("Id")!.SetValue(campaign, 10);
        campaign.ChangeStatus(TopUpCampaignStatusCodes.Active, 99, DateTime.UtcNow);

        return TopUpRun.CreateManual(
            campaign,
            "state-test-key",
            requestedByUserId: 99,
            requestedAtUtc: new DateTime(2026, 6, 17, 3, 0, 0, DateTimeKind.Utc),
            note: null);
    }
}
