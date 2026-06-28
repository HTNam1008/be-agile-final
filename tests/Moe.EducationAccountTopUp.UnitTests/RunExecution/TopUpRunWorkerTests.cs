using System.Reflection;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moe.Application.Abstractions.Clock;
using Moe.Modules.EducationAccountTopUp.Application.RunExecution;
using Moe.Modules.EducationAccountTopUp.Domain.TopUps;
using Moe.Modules.EducationAccountTopUp.IGateway;
using Moe.Modules.EducationAccountTopUp.IGateway.Repositories;
using Moe.Modules.EducationAccountTopUp.Infrastructure.TopUpRunDispatcher;
using Moe.SharedKernel.Results;
using Xunit;

namespace Moe.EducationAccountTopUp.UnitTests.RunExecution;

public sealed class TopUpRunWorkerTests
{
    [Fact]
    public async Task Should_Process_Enqueued_Run()
    {
        WorkerFixture fixture = new();
        TopUpRun run = fixture.AddRun();
        fixture.Resolver.SetRecipients(Recipients(1));
        TaskCompletionSource processed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Orchestrator.OnExecute = () => processed.TrySetResult();
        ChannelTopUpRunDispatcher dispatcher = new(NullLogger<ChannelTopUpRunDispatcher>.Instance);
        FakeClock clock = new(DateTimeOffset.UtcNow);
        TopUpRunWorker worker = new(dispatcher, fixture.ScopeFactory, NullLogger<TopUpRunWorker>.Instance, clock);

        await worker.StartAsync(CancellationToken.None);
        await dispatcher.EnqueueAsync(run.Id);

        await processed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await worker.StopAsync(CancellationToken.None);

        fixture.Orchestrator.Calls.Should().Be(1);
        fixture.Reconciliation.Calls.Should().Be(1);
    }

    [Fact]
    public async Task Should_Skip_Terminal_Run()
    {
        WorkerFixture fixture = new();
        TopUpRun run = fixture.AddRun();
        run.StartProcessing(DateTime.UtcNow).IsSuccess.Should().BeTrue();
        run.Finalize(0, 0, 0, 0, 0m, DateTime.UtcNow).IsSuccess.Should().BeTrue();

        await fixture.CreateWorker().ProcessRunAsync(run.Id);

        fixture.Orchestrator.Calls.Should().Be(0);
        fixture.Reconciliation.Calls.Should().Be(0);
    }

    [Fact]
    public async Task Should_Skip_Not_Found_Run()
    {
        WorkerFixture fixture = new();

        await fixture.CreateWorker().ProcessRunAsync(999);

        fixture.Orchestrator.Calls.Should().Be(0);
        fixture.Reconciliation.Calls.Should().Be(0);
    }

    [Fact]
    public async Task Should_Handle_Empty_Recipients()
    {
        WorkerFixture fixture = new();
        TopUpRun run = fixture.AddRun();
        fixture.Resolver.SetRecipients([]);

        await fixture.CreateWorker().ProcessRunAsync(run.Id);

        fixture.Orchestrator.Calls.Should().Be(1);
        fixture.Orchestrator.LastRecipients.Should().BeEmpty();
    }

    [Fact]
    public async Task Should_Process_In_Chunks()
    {
        WorkerFixture fixture = new();
        TopUpRun run = fixture.AddRun();
        fixture.Resolver.SetRecipients(Recipients(1500));

        await fixture.CreateWorker().ProcessRunAsync(run.Id);

        fixture.Resolver.ChunkOffsets.Should().Equal(0, 500, 1000);
        fixture.Orchestrator.LastRecipients.Should().HaveCount(1500);
    }

    [Fact]
    public async Task Should_Call_Reconciliation_After_Execution()
    {
        WorkerFixture fixture = new();
        TopUpRun run = fixture.AddRun();
        fixture.Resolver.SetRecipients(Recipients(1));
        fixture.Orchestrator.OnExecute = () => fixture.Reconciliation.Calls.Should().Be(0);

        await fixture.CreateWorker().ProcessRunAsync(run.Id);

        fixture.Orchestrator.Calls.Should().Be(1);
        fixture.Reconciliation.Calls.Should().Be(1);
    }

    [Fact]
    public async Task Should_Add_CorrelationId_Scope_When_Processing_Run()
    {
        WorkerFixture fixture = new();
        TopUpRun run = fixture.AddRun();
        fixture.Resolver.SetRecipients([]);
        TestLogger<TopUpRunWorker> logger = new();

        await fixture.CreateWorker(logger).ProcessRunAsync(run.Id);

        logger.Scopes.Should().Contain(scope => scope.Contains($"topup-run-{run.Id}"));
    }

    private static IReadOnlyList<RecipientInfo> Recipients(int count)
    {
        return Enumerable.Range(1, count)
            .Select(index => new RecipientInfo
            {
                EducationAccountId = index,
                Amount = 100m,
                OrganizationUnitId = 1,
                CampaignReason = "Campaign top-up"
            })
            .ToArray();
    }

    private sealed class WorkerFixture
    {
        private readonly ServiceProvider _provider;

        public FakeTopUpRunRepository Runs { get; } = new();
        public FakeRecipientResolver Resolver { get; } = new();
        public FakeRunExecutionOrchestrator Orchestrator { get; } = new();
        public FakeRunReconciliationService Reconciliation { get; } = new();
        public FakePendingTransactionRecoveryService Recovery { get; } = new();
        public IServiceScopeFactory ScopeFactory => _provider.GetRequiredService<IServiceScopeFactory>();

        public WorkerFixture()
        {
            ServiceCollection services = new();
            services.AddSingleton<ITopUpRunRepository>(Runs);
            services.AddSingleton<IRecipientResolver>(Resolver);
            services.AddSingleton<IRunExecutionOrchestrator>(Orchestrator);
            services.AddSingleton<IRunReconciliationService>(Reconciliation);
            services.AddSingleton<IPendingTransactionRecoveryService>(Recovery);
            _provider = services.BuildServiceProvider();
        }

        public TopUpRunWorker CreateWorker(ILogger<TopUpRunWorker>? logger = null)
        {
            FakeTopUpRunQueueReader reader = new();
            FakeClock clock = new(DateTimeOffset.UtcNow);
            return new TopUpRunWorker(reader, ScopeFactory, logger ?? NullLogger<TopUpRunWorker>.Instance, clock);
        }

        public TopUpRun AddRun()
        {
            DateTime now = DateTime.UtcNow;
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
                "INSTANT",
                100m,
                99,
                now);
            campaign.ChangeStatus(TopUpCampaignStatusCodes.Active, 99, now);
            TopUpRun run = TopUpRun.CreateManual(campaign, Guid.NewGuid().ToString("N"), 99, now, null);
            Runs.Add(run);
            return run;
        }
    }

    private sealed class FakeTopUpRunQueueReader : ITopUpRunQueueReader
    {
        private readonly System.Threading.Channels.Channel<long> _channel = System.Threading.Channels.Channel.CreateUnbounded<long>();
        public System.Threading.Channels.ChannelReader<long> Reader => _channel.Reader;
    }

    private sealed class FakeTopUpRunRepository : ITopUpRunRepository
    {
        private static readonly PropertyInfo IdProperty = typeof(TopUpRun).GetProperty(nameof(TopUpRun.Id))!;
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

        public Task<TopUpRun?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default) => Task.FromResult(_runs.Values.SingleOrDefault(x => x.IdempotencyKey == idempotencyKey));
        public Task<bool> ExistsForScheduledOccurrenceAsync(long campaignId, DateTime scheduledFor, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<bool> HasRunsForCampaignAsync(long campaignId, CancellationToken cancellationToken = default) => Task.FromResult(_runs.Values.Any(x => x.TopUpCampaignId == campaignId && x.RunStatusCode != TopUpRunStatusCodes.Failed));
        public Task AddAsync(TopUpRun run, CancellationToken cancellationToken = default) { Add(run); return Task.CompletedTask; }
    }

    private sealed class FakeRecipientResolver : IRecipientResolver
    {
        private IReadOnlyList<RecipientInfo> _recipients = [];
        public List<int> ChunkOffsets { get; } = [];

        public void SetRecipients(IReadOnlyList<RecipientInfo> recipients) => _recipients = recipients.OrderBy(x => x.EducationAccountId).ToArray();
        public Task<int> GetTotalRecipientCountAsync(long campaignId, long runId, CancellationToken cancellationToken = default) => Task.FromResult(_recipients.Count);

        public Task<IReadOnlyList<RecipientInfo>> GetRecipientsChunkAsync(long campaignId, long runId, int chunkSize, int offset, CancellationToken cancellationToken = default)
        {
            ChunkOffsets.Add(offset);
            IReadOnlyList<RecipientInfo> chunk = _recipients.Skip(offset).Take(chunkSize).ToArray();
            return Task.FromResult(chunk);
        }
    }

    private sealed class FakeRunExecutionOrchestrator : IRunExecutionOrchestrator
    {
        public int Calls { get; private set; }
        public IReadOnlyList<RecipientInfo> LastRecipients { get; private set; } = [];
        public Action? OnExecute { get; set; }

        public Task<Result<RunExecutionResult>> ExecuteRunAsync(long topUpRunId, IReadOnlyList<RecipientInfo> recipients, CancellationToken cancellationToken = default)
        {
            Calls++;
            LastRecipients = recipients;
            OnExecute?.Invoke();
            return Task.FromResult(Result<RunExecutionResult>.Success(new RunExecutionResult
            {
                TopUpRunId = topUpRunId,
                TerminalStatus = TopUpRunStatusCodes.Completed,
                TotalSelected = recipients.Count,
                TotalProcessed = recipients.Count,
                TotalSucceeded = recipients.Count,
                TotalFailed = 0,
                TotalSkipped = 0,
                TotalAmount = recipients.Sum(x => x.Amount)
            }));
        }

        public bool CancelRun(long topUpRunId)
        {
            // Fake implementation for tests
            return false;
        }
    }

    private sealed class FakeRunReconciliationService : IRunReconciliationService
    {
        public int Calls { get; private set; }

        public Task<Result<ReconciliationResult>> ReconcileRunAsync(long topUpRunId, CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(Result<ReconciliationResult>.Success(ReconciliationResult.Verified(
                topUpRunId,
                TopUpRunStatusCodes.Completed,
                new TransactionSummary
                {
                    TotalSelected = 0,
                    TotalProcessed = 0,
                    TotalSucceeded = 0,
                    TotalFailed = 0,
                    TotalSkipped = 0,
                    TotalPending = 0,
                    TotalAmount = 0m
                })));
        }
    }

    private sealed class FakePendingTransactionRecoveryService : IPendingTransactionRecoveryService
    {
        public Task<int> RecoverPendingTransactionsAsync(long topUpRunId, string campaignReason, CancellationToken cancellationToken = default)
            => Task.FromResult(0);
    }

    private sealed class FakeClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow { get; } = utcNow;
    }

    private sealed class TestLogger<T> : ILogger<T>
    {
        public List<string> Scopes { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            if (state is IEnumerable<KeyValuePair<string, object>> values)
            {
                Scopes.Add(string.Join(";", values.Select(value => $"{value.Key}={value.Value}")));
            }
            else
            {
                Scopes.Add(state?.ToString() ?? string.Empty);
            }

            return null;
        }

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        { }
    }
}
