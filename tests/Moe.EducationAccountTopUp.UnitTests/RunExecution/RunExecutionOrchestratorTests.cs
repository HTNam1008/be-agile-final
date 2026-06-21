using System.Reflection;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moe.Application.Abstractions.Clock;
using Moe.Application.Abstractions.Persistence;
using Moe.Modules.EducationAccountTopUp.Application.RunExecution;
using Moe.Modules.EducationAccountTopUp.Domain.TopUps;
using Moe.Modules.EducationAccountTopUp.IGateway;
using Moe.Modules.EducationAccountTopUp.IGateway.Repositories;
using Moe.SharedKernel.Results;
using Xunit;

namespace Moe.EducationAccountTopUp.UnitTests.RunExecution;

public sealed class RunExecutionOrchestratorTests
{
    private readonly FakeRecipientProcessingService _recipientProcessor = new();
    private readonly FakeTopUpRunRepository _runs = new();
    private readonly FakeTopUpTransactionRepository _transactions = new();
    private readonly FakeTopUpExecutionEventPublisher _events = new();
    private readonly FakeTopUpExecutionMetrics _metrics = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeClock _clock = new(new DateTimeOffset(2026, 6, 18, 4, 0, 0, TimeSpan.Zero));

    [Fact]
    public async Task Should_Complete_Run_When_All_Recipients_Succeed()
    {
        TopUpRun run = AddRun();
        _recipientProcessor.UseDefaultCompletedResult(100m);

        var result = await CreateOrchestrator().ExecuteRunAsync(run.Id, Recipients(3, 100m));

        result.IsSuccess.Should().BeTrue();
        result.Value.TerminalStatus.Should().Be(TopUpRunStatusCodes.Completed);
        result.Value.TotalSucceeded.Should().Be(3);
        result.Value.TotalFailed.Should().Be(0);
        run.RunStatusCode.Should().Be(TopUpRunStatusCodes.Completed);
    }

    [Fact]
    public async Task Should_Produce_Partial_Run_When_Mixed_Results()
    {
        TopUpRun run = AddRun();
        _recipientProcessor.EnqueueCompleted(100m);
        _recipientProcessor.EnqueueFailed(SafeReasons.CreditRejected);
        _recipientProcessor.EnqueueCompleted(100m);

        var result = await CreateOrchestrator().ExecuteRunAsync(run.Id, Recipients(3, 100m));

        result.IsSuccess.Should().BeTrue();
        result.Value.TerminalStatus.Should().Be(TopUpRunStatusCodes.Partial);
        result.Value.TotalSucceeded.Should().Be(2);
        result.Value.TotalFailed.Should().Be(1);
    }

    [Fact]
    public async Task Should_Produce_Failed_Run_When_All_Fail()
    {
        TopUpRun run = AddRun();
        _recipientProcessor.UseDefaultFailedResult(SafeReasons.CreditRejected);

        var result = await CreateOrchestrator().ExecuteRunAsync(run.Id, Recipients(3, 100m));

        result.IsSuccess.Should().BeTrue();
        result.Value.TerminalStatus.Should().Be(TopUpRunStatusCodes.Failed);
        result.Value.TotalSucceeded.Should().Be(0);
        result.Value.TotalFailed.Should().Be(3);
    }

    [Fact]
    public async Task Should_Skip_Closed_Account_And_Continue()
    {
        TopUpRun run = AddRun();
        _recipientProcessor.EnqueueCompleted(100m);
        _recipientProcessor.EnqueueSkipped(SafeReasons.AccountClosed);
        _recipientProcessor.EnqueueCompleted(100m);

        var result = await CreateOrchestrator().ExecuteRunAsync(run.Id, Recipients(3, 100m));

        result.IsSuccess.Should().BeTrue();
        result.Value.TerminalStatus.Should().Be(TopUpRunStatusCodes.Partial);
        result.Value.TotalSkipped.Should().Be(1);
        result.Value.TotalSucceeded.Should().Be(2);
    }

    [Fact]
    public async Task Should_Retry_Transient_Failure_And_Succeed()
    {
        TopUpRun run = AddRun();
        _recipientProcessor.EnqueueFailed(SafeReasons.CreditServiceUnavailable);
        _recipientProcessor.EnqueueCompleted(100m);

        var result = await CreateOrchestrator().ExecuteRunAsync(run.Id, Recipients(1, 100m));

        result.IsSuccess.Should().BeTrue();
        result.Value.TotalSucceeded.Should().Be(1);
        _recipientProcessor.Calls.Should().Be(2);
    }

    [Fact]
    public async Task Should_Fail_After_Exhausting_Transient_Retries()
    {
        TopUpRun run = AddRun();
        _recipientProcessor.UseDefaultFailedResult(SafeReasons.CreditServiceUnavailable);

        var result = await CreateOrchestrator().ExecuteRunAsync(run.Id, Recipients(1, 100m));

        result.IsSuccess.Should().BeTrue();
        result.Value.TerminalStatus.Should().Be(TopUpRunStatusCodes.Failed);
        result.Value.TotalFailed.Should().Be(1);
        _recipientProcessor.Calls.Should().Be(3);
    }

    [Fact]
    public async Task Should_Not_Retry_Permanent_Failure()
    {
        TopUpRun run = AddRun();
        _recipientProcessor.UseDefaultFailedResult(SafeReasons.CreditRejected);

        var result = await CreateOrchestrator().ExecuteRunAsync(run.Id, Recipients(1, 100m));

        result.IsSuccess.Should().BeTrue();
        result.Value.TotalFailed.Should().Be(1);
        _recipientProcessor.Calls.Should().Be(1);
    }

    [Fact]
    public async Task Should_Continue_Processing_After_Recipient_Failure()
    {
        TopUpRun run = AddRun();
        _recipientProcessor.EnqueueFailed(SafeReasons.CreditRejected);
        _recipientProcessor.EnqueueCompleted(100m);
        _recipientProcessor.EnqueueCompleted(100m);

        var result = await CreateOrchestrator().ExecuteRunAsync(run.Id, Recipients(3, 100m));

        result.IsSuccess.Should().BeTrue();
        result.Value.TotalSucceeded.Should().Be(2);
        result.Value.TotalFailed.Should().Be(1);
        _recipientProcessor.Calls.Should().Be(3);
    }

    [Fact]
    public async Task Should_Transition_Run_To_Processing_Before_Recipients()
    {
        TopUpRun run = AddRun();
        _recipientProcessor.OnProcess = () => run.RunStatusCode.Should().Be(TopUpRunStatusCodes.Processing);
        _recipientProcessor.UseDefaultCompletedResult(100m);

        var result = await CreateOrchestrator().ExecuteRunAsync(run.Id, Recipients(1, 100m));

        result.IsSuccess.Should().BeTrue();
        _unitOfWork.SaveCalls.Should().BeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task Should_Finalize_Run_With_Correct_Totals()
    {
        TopUpRun run = AddRun();
        _recipientProcessor.EnqueueCompleted(100m);
        _recipientProcessor.EnqueueCompleted(100m);
        _recipientProcessor.EnqueueCompleted(100m);
        _recipientProcessor.EnqueueFailed(SafeReasons.CreditRejected);
        _recipientProcessor.EnqueueSkipped(SafeReasons.AccountClosed);

        var result = await CreateOrchestrator().ExecuteRunAsync(run.Id, Recipients(5, 100m));

        result.IsSuccess.Should().BeTrue();
        result.Value.TotalProcessed.Should().Be(5);
        result.Value.TotalSucceeded.Should().Be(3);
        result.Value.TotalFailed.Should().Be(1);
        result.Value.TotalSkipped.Should().Be(1);
        result.Value.TotalAmount.Should().Be(300m);
    }

    [Fact]
    public async Task Should_Handle_Already_Processing_Run_On_Restart()
    {
        TopUpRun run = AddRun();
        run.StartProcessing(_clock.UtcNow.UtcDateTime).IsSuccess.Should().BeTrue();
        run.ClearDomainEvents();
        _unitOfWork.SaveCalls = 0;
        _recipientProcessor.UseDefaultCompletedResult(100m);

        var result = await CreateOrchestrator().ExecuteRunAsync(run.Id, Recipients(1, 100m));

        result.IsSuccess.Should().BeTrue();
        result.Value.TerminalStatus.Should().Be(TopUpRunStatusCodes.Completed);
        _unitOfWork.SaveCalls.Should().Be(1);
    }

    [Fact]
    public async Task Should_Return_Error_When_Run_Not_Found()
    {
        var result = await CreateOrchestrator().ExecuteRunAsync(999, Recipients(1, 100m));

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(TopUpErrors.RunNotFound);
        _recipientProcessor.Calls.Should().Be(0);
    }

    [Fact]
    public async Task Should_Emit_Run_Events_After_Commit_Boundaries()
    {
        TopUpRun run = AddRun();
        _recipientProcessor.UseDefaultCompletedResult(100m);
        _events.OnStarted = () => _unitOfWork.SaveCalls.Should().Be(1);
        _events.OnCompleted = () => _unitOfWork.SaveCalls.Should().Be(2);

        var result = await CreateOrchestrator().ExecuteRunAsync(run.Id, Recipients(1, 100m));

        result.IsSuccess.Should().BeTrue();
        _events.Emitted.Should().Equal("started", "completed");
    }

    [Fact]
    public async Task Should_Record_Run_Metrics()
    {
        TopUpRun run = AddRun();
        _recipientProcessor.UseDefaultCompletedResult(100m);

        await CreateOrchestrator().ExecuteRunAsync(run.Id, Recipients(2, 100m));

        _metrics.StartedRecords.Should().ContainSingle();
        _metrics.CompletedRecords.Should().ContainSingle();
        _metrics.CompletedRecords.Single().TotalProcessed.Should().Be(2);
    }

    private RunExecutionOrchestrator CreateOrchestrator()
    {
        return new RunExecutionOrchestrator(
            _recipientProcessor,
            new FakeTopUpCampaignRepository(),
            _runs,
            _transactions,
            _events,
            _metrics,
            _unitOfWork,
            _clock,
            NullLogger<RunExecutionOrchestrator>.Instance);
    }

    private TopUpRun AddRun()
    {
        TopUpRun run = TopUpRun.CreateManual(
            CreateActiveCampaign(),
            $"run-key-{Guid.NewGuid():N}",
            requestedByUserId: 99,
            requestedAtUtc: _clock.UtcNow.UtcDateTime,
            note: null);

        _runs.Add(run);
        return run;
    }

    private static TopUpCampaign CreateActiveCampaign()
    {
        DateTime now = new(2026, 6, 18, 4, 0, 0, DateTimeKind.Utc);
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
        return campaign;
    }

    private static IReadOnlyList<RecipientInfo> Recipients(int count, decimal amount)
    {
        return Enumerable.Range(1, count)
            .Select(index => new RecipientInfo
            {
                EducationAccountId = index,
                Amount = amount,
                OrganizationUnitId = 1,
                CampaignReason = "Campaign top-up"
            })
            .ToArray();
    }

    private sealed class FakeRecipientProcessingService : IRecipientProcessingService
    {
        private readonly Queue<Result<RecipientProcessingResult>> _results = new();
        private Result<RecipientProcessingResult>? _defaultResult;

        public int Calls { get; private set; }
        public Action? OnProcess { get; set; }

        public void EnqueueCompleted(decimal amount)
        {
            _results.Enqueue(Result<RecipientProcessingResult>.Success(
                RecipientProcessingResult.Completed(Calls + _results.Count + 1, 1000 + _results.Count, amount, false)));
        }

        public void EnqueueFailed(string reason)
        {
            _results.Enqueue(Result<RecipientProcessingResult>.Success(
                RecipientProcessingResult.Failed(Calls + _results.Count + 1, reason)));
        }

        public void EnqueueSkipped(string reason)
        {
            _results.Enqueue(Result<RecipientProcessingResult>.Success(
                RecipientProcessingResult.Skipped(Calls + _results.Count + 1, reason)));
        }

        public void UseDefaultCompletedResult(decimal amount)
        {
            _defaultResult = Result<RecipientProcessingResult>.Success(
                RecipientProcessingResult.Completed(1, 1000, amount, false));
        }

        public void UseDefaultFailedResult(string reason)
        {
            _defaultResult = Result<RecipientProcessingResult>.Success(
                RecipientProcessingResult.Failed(1, reason));
        }

        public Task<Result<RecipientProcessingResult>> ProcessRecipientAsync(
            long topUpRunId,
            long educationAccountId,
            decimal amount,
            long organizationUnitId,
            string campaignReason,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            OnProcess?.Invoke();

            if (_results.Count > 0)
            {
                return Task.FromResult(_results.Dequeue());
            }

            return Task.FromResult(_defaultResult ?? Result<RecipientProcessingResult>.Success(
                RecipientProcessingResult.Completed(Calls, 1000 + Calls, amount, false)));
        }
    }

    private sealed class FakeTopUpRunRepository : ITopUpRunRepository
    {
        private static readonly PropertyInfo IdProperty =
            typeof(TopUpRun).GetProperty(nameof(TopUpRun.Id))!;

        private long _nextId = 1;
        private readonly Dictionary<long, TopUpRun> _runs = [];

        public void Add(TopUpRun run)
        {
            IdProperty.SetValue(run, _nextId++);
            _runs[run.Id] = run;
        }

        public Task<TopUpRun?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
        {
            _runs.TryGetValue(id, out TopUpRun? run);
            return Task.FromResult(run);
        }

        public Task<TopUpRun?> GetByIdempotencyKeyAsync(
            string idempotencyKey,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_runs.Values.SingleOrDefault(x => x.IdempotencyKey == idempotencyKey));
        }

        public Task<bool> ExistsForScheduledOccurrenceAsync(
            long campaignId,
            DateTime scheduledFor,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_runs.Values.Any(
                x => x.TopUpCampaignId == campaignId && x.ScheduledForUtc == scheduledFor));
        }

        public Task<bool> HasRunsForCampaignAsync(long campaignId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_runs.Values.Any(
                x => x.TopUpCampaignId == campaignId && x.RunStatusCode != TopUpRunStatusCodes.Failed));
        }

        public Task AddAsync(TopUpRun run, CancellationToken cancellationToken = default)
        {
            Add(run);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeTopUpTransactionRepository : ITopUpTransactionRepository
    {
        public Task<TopUpTransaction?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
            => Task.FromResult<TopUpTransaction?>(null);

        public Task<TopUpTransaction?> GetByIdempotencyKeyAsync(
            string idempotencyKey,
            CancellationToken cancellationToken = default)
            => Task.FromResult<TopUpTransaction?>(null);

        public Task<TopUpTransaction?> GetByRunAndAccountAsync(
            long topUpRunId,
            long educationAccountId,
            CancellationToken cancellationToken = default)
            => Task.FromResult<TopUpTransaction?>(null);

        public Task<List<TopUpTransaction>> GetByRunIdAsync(
            long topUpRunId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new List<TopUpTransaction>());

        public void Add(TopUpTransaction transaction) { }

        public Task AddAsync(TopUpTransaction transaction, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public int SaveCalls { get; set; }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveCalls++;
            return Task.FromResult(1);
        }
    }

    private sealed class FakeClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow { get; } = utcNow;
    }

    private sealed class FakeTopUpExecutionEventPublisher : ITopUpExecutionEventPublisher
    {
        public List<string> Emitted { get; } = [];
        public Action? OnStarted { get; set; }
        public Action? OnCompleted { get; set; }

        public Task PublishRunStartedAsync(
            TopUpRunStartedReport report,
            CancellationToken cancellationToken = default)
        {
            OnStarted?.Invoke();
            Emitted.Add("started");
            return Task.CompletedTask;
        }

        public Task PublishRunCompletedAsync(
            TopUpRunCompletedReport report,
            CancellationToken cancellationToken = default)
        {
            OnCompleted?.Invoke();
            Emitted.Add("completed");
            return Task.CompletedTask;
        }

        public Task PublishTopUpReceivedAsync(
            TopUpReceivedReport report,
            CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class FakeTopUpExecutionMetrics : ITopUpExecutionMetrics
    {
        public List<(long TopUpRunId, long CampaignId, int TotalSelected)> StartedRecords { get; } = [];
        public List<(long TopUpRunId, long CampaignId, string TerminalStatus, int TotalProcessed)> CompletedRecords { get; } = [];

        public void RecordRunStarted(long topUpRunId, long campaignId, int totalSelected)
            => StartedRecords.Add((topUpRunId, campaignId, totalSelected));

        public void RecordRunCompleted(
            long topUpRunId,
            long campaignId,
            string terminalStatus,
            int totalProcessed,
            int totalSucceeded,
            int totalFailed,
            int totalSkipped,
            TimeSpan duration)
            => CompletedRecords.Add((topUpRunId, campaignId, terminalStatus, totalProcessed));

        public void RecordRecipientProcessed(
            long topUpRunId,
            string status,
            bool duplicateIdempotencyHit,
            bool accountCreditFailure)
        { }

        public void RecordAccountCreditDbConflict() { }
    }

    private sealed class FakeTopUpCampaignRepository : ITopUpCampaignRepository
    {
        public Task AddAsync(TopUpCampaign campaign, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task AddRecipientAsync(TopUpCampaignRecipient recipient, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task AddRuleAsync(TopUpCampaignRule rule, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<bool> CampaignCodeExistsAsync(long organizationId, string campaignCode, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<int> CountActiveRecipientsAsync(long campaignId, CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task<int> CountActiveRulesAsync(long campaignId, CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task<TopUpCampaign?> GetByIdAsync(long id, CancellationToken cancellationToken = default) => Task.FromResult<TopUpCampaign?>(CreateActiveCampaign());
        public Task<IReadOnlyList<TopUpCampaign>> GetDueCampaignsAsync(DateTime utcNow, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<TopUpCampaign>>([]);
        public Task<IReadOnlyList<TopUpCampaignRecipient>> GetRecipientsAsync(long campaignId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<TopUpCampaignRecipient>>([]);
        public Task<IReadOnlyList<TopUpCampaignRule>> GetRulesAsync(long campaignId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<TopUpCampaignRule>>([]);
        public Task RemoveRecipientsAsync(IEnumerable<TopUpCampaignRecipient> recipients, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RemoveRulesAsync(IEnumerable<TopUpCampaignRule> rules, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
