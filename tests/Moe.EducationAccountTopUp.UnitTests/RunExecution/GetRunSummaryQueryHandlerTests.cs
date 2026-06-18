using System.Reflection;
using FluentAssertions;
using Moe.Modules.EducationAccountTopUp.Application.RunExecution.GetRunSummary;
using Moe.Modules.EducationAccountTopUp.Domain.TopUps;
using Moe.Modules.EducationAccountTopUp.IGateway.Repositories;
using Xunit;

namespace Moe.EducationAccountTopUp.UnitTests.RunExecution;

public sealed class GetRunSummaryQueryHandlerTests
{
    private readonly FakeTopUpRunRepository _runs = new();

    [Fact]
    public async Task Should_Return_Run_Summary()
    {
        DateTime now = new(2026, 6, 18, 4, 0, 0, DateTimeKind.Utc);
        TopUpRun run = CreateRun(now);
        run.StartProcessing(now.AddMinutes(1)).IsSuccess.Should().BeTrue();
        run.SetTotalSelected(2).IsSuccess.Should().BeTrue();
        run.Finalize(2, 2, 0, 0, 200m, now.AddMinutes(2)).IsSuccess.Should().BeTrue();
        _runs.Add(run);

        GetRunSummaryQueryHandler handler = new(_runs);
        var result = await handler.Handle(new GetRunSummaryQuery(run.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.TopUpRunId.Should().Be(run.Id);
        result.Value.CampaignId.Should().Be(run.TopUpCampaignId);
        result.Value.RunStatus.Should().Be(TopUpRunStatusCodes.Completed);
        result.Value.TriggerType.Should().Be(TopUpRunTriggerTypes.Manual);
        result.Value.TotalSelected.Should().Be(2);
        result.Value.TotalProcessed.Should().Be(2);
        result.Value.TotalSucceeded.Should().Be(2);
        result.Value.TotalFailed.Should().Be(0);
        result.Value.TotalSkipped.Should().Be(0);
        result.Value.TotalAmount.Should().Be(200m);
        result.Value.RequestedAtUtc.Should().Be(now);
        result.Value.StartedAt.Should().Be(now.AddMinutes(1));
        result.Value.CompletedAt.Should().Be(now.AddMinutes(2));
        result.Value.Note.Should().Be("Manual request");
    }

    [Fact]
    public async Task Should_Return_Error_When_Run_Not_Found()
    {
        GetRunSummaryQueryHandler handler = new(_runs);

        var result = await handler.Handle(new GetRunSummaryQuery(999), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(TopUpErrors.RunNotFound);
    }

    private static TopUpRun CreateRun(DateTime now)
    {
        TopUpCampaign campaign = TopUpCampaign.Create(
            1,
            "CAMPAIGN-01",
            "Test campaign",
            null,
            "FIXED",
            100m,
            "Campaign top-up",
            "IMMEDIATE",
            new DateOnly(2026, 1, 1),
            null,
            null,
            null,
            99,
            now);
        campaign.ChangeStatus(TopUpCampaignStatusCodes.Active, 99, now);

        return TopUpRun.CreateManual(
            campaign,
            "summary-key",
            99,
            now,
            "Manual request");
    }

    private sealed class FakeTopUpRunRepository : ITopUpRunRepository
    {
        private static readonly PropertyInfo IdProperty =
            typeof(TopUpRun).GetProperty(nameof(TopUpRun.Id))!;

        private readonly Dictionary<long, TopUpRun> _runs = [];

        public void Add(TopUpRun run)
        {
            IdProperty.SetValue(run, 123);
            _runs[run.Id] = run;
        }

        public Task<TopUpRun?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
        {
            _runs.TryGetValue(id, out TopUpRun? run);
            return Task.FromResult(run);
        }

        public Task<TopUpRun?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default)
            => Task.FromResult(_runs.Values.SingleOrDefault(x => x.IdempotencyKey == idempotencyKey));

        public Task<bool> ExistsForScheduledOccurrenceAsync(long campaignId, DateTime scheduledFor, CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task AddAsync(TopUpRun run, CancellationToken cancellationToken = default)
        {
            Add(run);
            return Task.CompletedTask;
        }
    }
}
