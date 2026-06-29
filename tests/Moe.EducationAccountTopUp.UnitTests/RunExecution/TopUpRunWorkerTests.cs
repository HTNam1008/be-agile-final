using System.Reflection;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moe.Application.Abstractions.Clock;
using Moe.Application.Abstractions.Persistence;
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
        fixture.Orchestrator.OnProcessChunk = () => processed.TrySetResult();
        ChannelTopUpRunDispatcher dispatcher = new(NullLogger<ChannelTopUpRunDispatcher>.Instance);
        FakeClock clock = new(DateTimeOffset.UtcNow);
        TopUpRunWorker worker = new(dispatcher, fixture.ScopeFactory, NullLogger<TopUpRunWorker>.Instance, clock);

        await worker.StartAsync(CancellationToken.None);
        await dispatcher.EnqueueAsync(run.Id);

        await processed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await worker.StopAsync(CancellationToken.None);

        fixture.Orchestrator.ChunkCalls.Should().Be(1);
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

        fixture.Orchestrator.ChunkCalls.Should().Be(0);
        fixture.Reconciliation.Calls.Should().Be(0);
    }

    [Fact]
    public async Task Should_Skip_Not_Found_Run()
    {
        WorkerFixture fixture = new();

        await fixture.CreateWorker().ProcessRunAsync(999);

        fixture.Orchestrator.ChunkCalls.Should().Be(0);
        fixture.Reconciliation.Calls.Should().Be(0);
    }

    [Fact]
    public async Task Should_Handle_Empty_Recipients()
    {
        WorkerFixture fixture = new();
        TopUpRun run = fixture.AddRun();
        fixture.Resolver.SetRecipients([]);

        await fixture.CreateWorker().ProcessRunAsync(run.Id);

        fixture.Orchestrator.ChunkCalls.Should().Be(0);
        run.RunStatusCode.Should().Be(TopUpRunStatusCodes.Failed);
        run.TotalSucceeded.Should().Be(0);
    }

    [Fact]
    public async Task Should_Process_In_Chunks()
    {
        WorkerFixture fixture = new();
        TopUpRun run = fixture.AddRun();
        fixture.Resolver.SetRecipients(Recipients(1500));

        await fixture.CreateWorker().ProcessRunAsync(run.Id);

        fixture.Resolver.ChunkOffsets.Should().Equal(0, 500, 1000);
        fixture.Orchestrator.ChunkCalls.Should().Be(3);
        var completedRun = await fixture.Runs.GetByIdAsync(run.Id);
        completedRun!.TotalSucceeded.Should().Be(1500);
        completedRun!.TotalAmount.Should().Be(150000m);
    }

    [Fact]
    public async Task Should_Call_Reconciliation_After_Execution()
    {
        WorkerFixture fixture = new();
        TopUpRun run = fixture.AddRun();
        fixture.Resolver.SetRecipients(Recipients(1));
        fixture.Orchestrator.OnProcessChunk = () => fixture.Reconciliation.Calls.Should().Be(0);

        await fixture.CreateWorker().ProcessRunAsync(run.Id);

        fixture.Orchestrator.ChunkCalls.Should().Be(1);
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
        public FakeTopUpCampaignRepository Campaigns { get; } = new();
        public FakeDynamicTopUpContractRepository ContractRepo { get; } = new();
        public FakeRecipientResolver Resolver { get; } = new();
        public FakeRunExecutionOrchestrator Orchestrator { get; } = new();
        public FakeRunReconciliationService Reconciliation { get; } = new();
        public FakePendingTransactionRecoveryService Recovery { get; } = new();
        public FakeUnitOfWork UnitOfWork { get; } = new();
        public IServiceScopeFactory ScopeFactory => _provider.GetRequiredService<IServiceScopeFactory>();

        public WorkerFixture()
        {
            ServiceCollection services = new();
            services.AddSingleton<ITopUpRunRepository>(Runs);
            services.AddSingleton<ITopUpCampaignRepository>(Campaigns);
            services.AddSingleton<ITopUpTransactionRepository>(new FakeTransactionRepository());
            services.AddSingleton<IDynamicTopUpContractRepository>(ContractRepo);
            services.AddSingleton<IRecipientResolver>(Resolver);
            services.AddSingleton<IRunExecutionOrchestrator>(Orchestrator);
            services.AddSingleton<IRunReconciliationService>(Reconciliation);
            services.AddSingleton<IPendingTransactionRecoveryService>(Recovery);
            services.AddSingleton<IUnitOfWork>(UnitOfWork);
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
            Campaigns.Add(campaign);
            TopUpRun run = TopUpRun.CreateManual(campaign, Guid.NewGuid().ToString("N"), 99, now, null);
            Runs.Add(run);
            return run;
        }
    }

    private sealed class FakeTransactionRepository : ITopUpTransactionRepository
    {
        public Task<TopUpTransaction?> GetByIdAsync(long id, CancellationToken cancellationToken = default) => Task.FromResult<TopUpTransaction?>(null);
        public Task<TopUpTransaction?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default) => Task.FromResult<TopUpTransaction?>(null);
        public Task<TopUpTransaction?> GetByRunAndAccountAsync(long topUpRunId, long educationAccountId, CancellationToken cancellationToken = default) => Task.FromResult<TopUpTransaction?>(null);
        public Task<List<TopUpTransaction>> GetByRunIdAsync(long topUpRunId, CancellationToken cancellationToken = default) => Task.FromResult(new List<TopUpTransaction>());
        public Task<IReadOnlyList<TopUpTransaction>> GetPendingByRunIdPagedAsync(long topUpRunId, int skip, int take, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<TopUpTransaction>>([]);
        public Task<decimal> GetTotalDisbursedForCampaignAsync(long campaignId, CancellationToken cancellationToken = default) => Task.FromResult(0m);
        public void Add(TopUpTransaction transaction) { }
        public Task AddAsync(TopUpTransaction transaction, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeTopUpRunQueueReader : ITopUpRunQueueReader
    {
        private readonly System.Threading.Channels.Channel<long> _channel = System.Threading.Channels.Channel.CreateUnbounded<long>();
        public System.Threading.Channels.ChannelReader<long> Reader => _channel.Reader;
    }

    private sealed class FakeTopUpCampaignRepository : ITopUpCampaignRepository
    {
        private readonly Dictionary<long, TopUpCampaign> _campaigns = [];
        public void Add(TopUpCampaign campaign) => _campaigns[campaign.Id] = campaign;
        public Task<TopUpCampaign?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
        {
            _campaigns.TryGetValue(id, out TopUpCampaign? campaign);
            return Task.FromResult(campaign);
        }
        public Task<bool> CampaignCodeExistsAsync(long organizationId, string campaignCode, CancellationToken cancellationToken = default)
            => Task.FromResult(_campaigns.Values.Any(c => c.OrganizationId == organizationId && c.CampaignCode == campaignCode));
        public Task<IReadOnlyList<TopUpCampaign>> GetDueCampaignsAsync(DateTime utcNow, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<TopUpCampaign>>([]);
        public Task<IReadOnlyList<TopUpCampaign>> GetDueForAssessmentAsync(DateOnly today, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<TopUpCampaign>>([]);
        public Task<int> CountActiveRulesAsync(long campaignId, CancellationToken cancellationToken = default)
            => Task.FromResult(0);
        public Task<int> CountActiveRecipientsAsync(long campaignId, CancellationToken cancellationToken = default)
            => Task.FromResult(0);
        public Task<IReadOnlyList<TopUpCampaignRule>> GetRulesAsync(long campaignId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<TopUpCampaignRule>>([]);
        public Task RemoveRulesAsync(IEnumerable<TopUpCampaignRule> rules, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
        public Task AddRuleAsync(TopUpCampaignRule rule, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
        public Task<IReadOnlyList<TopUpCampaignRecipient>> GetRecipientsAsync(long campaignId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<TopUpCampaignRecipient>>([]);
        public Task<Dictionary<long, decimal>> GetAmountOverridesByCampaignAsync(long campaignId, CancellationToken cancellationToken = default)
            => Task.FromResult(new Dictionary<long, decimal>());
        public Task RemoveRecipientsAsync(IEnumerable<TopUpCampaignRecipient> recipients, long userId, DateTime nowUtc, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
        public Task AddRecipientAsync(TopUpCampaignRecipient recipient, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
        public Task AddAsync(TopUpCampaign campaign, CancellationToken cancellationToken = default)
        {
            Add(campaign);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeDynamicTopUpContractRepository : IDynamicTopUpContractRepository
    {
        public Task<DynamicTopUpContract?> GetByCampaignAndAccountAsync(long campaignId, long accountId, CancellationToken cancellationToken = default)
            => Task.FromResult<DynamicTopUpContract?>(null);
        public Task<IReadOnlyList<DynamicTopUpContract>> GetActiveByCampaignAsync(long campaignId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<DynamicTopUpContract>>([]);
        public Task<IReadOnlyList<DynamicTopUpContract>> GetDueForPaymentAsync(DateTime nowUtc, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<DynamicTopUpContract>>([]);
        public Task AddAsync(DynamicTopUpContract contract, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
        public Task<IReadOnlyList<DynamicTopUpContract>> GetActiveByCampaignAndAccountsAsync(long campaignId, IEnumerable<long> accountIds, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<DynamicTopUpContract>>([]);
        public Task SuspendNonQualifyingContractsAsync(long campaignId, IEnumerable<long> qualifyingAccountIds, DateTime suspendedAtUtc, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
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
        public Task<bool> HasActiveRunsForCampaignAsync(long campaignId, CancellationToken cancellationToken = default) => Task.FromResult(_runs.Values.Any(x => x.TopUpCampaignId == campaignId && (x.RunStatusCode == TopUpRunStatusCodes.Previewed || x.RunStatusCode == TopUpRunStatusCodes.Processing)));
        public Task AddAsync(TopUpRun run, CancellationToken cancellationToken = default) { Add(run); return Task.CompletedTask; }
    }

    private sealed class FakeRecipientResolver : IRecipientResolver
    {
        private IReadOnlyList<RecipientInfo> _recipients = [];
        public List<int> ChunkOffsets { get; } = [];

        public void SetRecipients(IReadOnlyList<RecipientInfo> recipients) => _recipients = recipients.OrderBy(x => x.EducationAccountId).ToArray();
        public Task<int> GetTotalRecipientCountAsync(long campaignId, long runId, CancellationToken cancellationToken = default) => Task.FromResult(_recipients.Count);
        public Task<decimal> GetTotalResolvedAmountAsync(long campaignId, long runId, CancellationToken cancellationToken = default) => Task.FromResult(_recipients.Sum(r => r.Amount));

        public Task<IReadOnlyList<RecipientInfo>> GetRecipientsChunkAsync(long campaignId, long runId, int chunkSize, int offset, CancellationToken cancellationToken = default)
        {
            ChunkOffsets.Add(offset);
            IReadOnlyList<RecipientInfo> chunk = _recipients.Skip(offset).Take(chunkSize).ToArray();
            return Task.FromResult(chunk);
        }
    }

    private sealed class FakeRunExecutionOrchestrator : IRunExecutionOrchestrator
    {
        public int ChunkCalls { get; private set; }
        public IReadOnlyList<RecipientInfo> LastChunkRecipients { get; private set; } = [];
        public Action? OnProcessChunk { get; set; }

        public Task<Result<RunExecutionResult>> ExecuteRunAsync(long topUpRunId, IReadOnlyList<RecipientInfo> recipients, CancellationToken cancellationToken = default)
        {
            OnProcessChunk?.Invoke();
            return Task.FromResult(Result<RunExecutionResult>.Success(new RunExecutionResult
            {
                TopUpRunId = topUpRunId,
                TerminalStatus = TopUpRunStatusCodes.Completed,
                TotalSelected = recipients.Count,
                TotalProcessed = recipients.Count,
                TotalSucceeded = recipients.Count,
                TotalFailed = 0,
                TotalSkipped = 0,
                TotalAmount = recipients.Sum(r => r.Amount),
                SuccessfulAccountIds = recipients.Select(r => r.EducationAccountId).ToList()
            }));
        }

        public Task<Result<ChunkProcessingResult>> ProcessChunkAsync(
            long topUpRunId,
            IReadOnlyList<RecipientInfo> chunk,
            ChunkProcessingAccumulator accumulator,
            CancellationToken cancellationToken = default)
        {
            ChunkCalls++;
            LastChunkRecipients = chunk;
            OnProcessChunk?.Invoke();
            accumulator.TotalSucceeded += chunk.Count;
            accumulator.TotalAmount += chunk.Sum(r => r.Amount);
            foreach (var r in chunk) accumulator.SuccessfulAccountIds.Add(r.EducationAccountId);
            return Task.FromResult(Result<ChunkProcessingResult>.Success(new ChunkProcessingResult(
                chunk.Count, 0, 0, chunk.Sum(r => r.Amount),
                chunk.Select(r => r.EducationAccountId).ToList())));
        }

        public void RegisterCancellationToken(long topUpRunId, CancellationTokenSource cts) { }
        public void UnregisterCancellationToken(long topUpRunId) { }

        public bool CancelRun(long topUpRunId)
        {
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

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public int SaveCalls { get; private set; }
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveCalls++;
            return Task.FromResult(1);
        }
    }
}
