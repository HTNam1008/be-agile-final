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

public sealed class PendingTransactionRecoveryTests
{
    private readonly FakeTopUpTransactionRepository _transactions = new();
    private readonly FakeTopUpRunRepository _runs = new();
    private readonly FakeAccountCreditGateway _accountGateway = new();
    private readonly FakeTopUpExecutionEventPublisher _events = new();
    private readonly FakeTopUpExecutionMetrics _metrics = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeClock _clock = new(new DateTimeOffset(2026, 6, 18, 4, 0, 0, TimeSpan.Zero));

    [Fact]
    public async Task Should_Recover_Pending_Transaction_With_Existing_Credit()
    {
        TopUpTransaction transaction = Pending(42, 100, 50m);
        _transactions.Seed(transaction);
        _accountGateway.EnqueueSuccess(9001, alreadyProcessed: true);

        int recovered = await CreateService().RecoverPendingTransactionsAsync(42, "Campaign top-up");

        recovered.Should().Be(1);
        transaction.TransactionStatusCode.Should().Be(TopUpTransactionStatusCodes.Completed);
        transaction.AccountTransactionId.Should().Be(9001);
    }

    [Fact]
    public async Task Should_Complete_Pending_Transaction_With_New_Credit()
    {
        TopUpTransaction transaction = Pending(42, 100, 50m);
        _transactions.Seed(transaction);
        _accountGateway.EnqueueSuccess(9002, alreadyProcessed: false);

        int recovered = await CreateService().RecoverPendingTransactionsAsync(42, "Campaign top-up");

        recovered.Should().Be(1);
        transaction.TransactionStatusCode.Should().Be(TopUpTransactionStatusCodes.Completed);
        transaction.AccountTransactionId.Should().Be(9002);
        _events.TopUpReceivedReports.Should().ContainSingle();
    }

    [Fact]
    public async Task Should_Fail_Pending_Transaction_When_Credit_Fails()
    {
        TopUpTransaction transaction = Pending(42, 100, 50m);
        _transactions.Seed(transaction);
        _accountGateway.EnqueueFailure(new Error("Account.CreditRejected", SafeReasons.CreditRejected));

        int recovered = await CreateService().RecoverPendingTransactionsAsync(42, "Campaign top-up");

        recovered.Should().Be(0);
        transaction.TransactionStatusCode.Should().Be(TopUpTransactionStatusCodes.Failed);
        transaction.Reason.Should().Be(SafeReasons.CreditRejected);
    }

    [Fact]
    public async Task Should_Skip_Non_Pending_Transactions()
    {
        TopUpTransaction completed = Pending(42, 100, 50m);
        completed.Complete(9001, _clock.UtcNow.UtcDateTime).IsSuccess.Should().BeTrue();
        TopUpTransaction pending = Pending(42, 101, 50m);
        _transactions.Seed(completed, pending);
        _accountGateway.EnqueueSuccess(9002, alreadyProcessed: false);

        int recovered = await CreateService().RecoverPendingTransactionsAsync(42, "Campaign top-up");

        recovered.Should().Be(1);
        _accountGateway.Calls.Should().Be(1);
        pending.TransactionStatusCode.Should().Be(TopUpTransactionStatusCodes.Completed);
    }

    [Fact]
    public async Task Should_Return_Zero_When_No_Pending()
    {
        TopUpTransaction completed = Pending(42, 100, 50m);
        completed.Complete(9001, _clock.UtcNow.UtcDateTime).IsSuccess.Should().BeTrue();
        _transactions.Seed(completed);

        int recovered = await CreateService().RecoverPendingTransactionsAsync(42, "Campaign top-up");

        recovered.Should().Be(0);
        _accountGateway.Calls.Should().Be(0);
    }

    [Fact]
    public async Task Should_Continue_After_Individual_Recovery_Failure()
    {
        TopUpTransaction first = Pending(42, 100, 50m);
        TopUpTransaction second = Pending(42, 101, 50m);
        TopUpTransaction third = Pending(42, 102, 50m);
        _transactions.Seed(first, second, third);
        _accountGateway.EnqueueSuccess(9001, alreadyProcessed: false);
        _accountGateway.EnqueueException();
        _accountGateway.EnqueueSuccess(9003, alreadyProcessed: false);

        int recovered = await CreateService().RecoverPendingTransactionsAsync(42, "Campaign top-up");

        recovered.Should().Be(2);
        first.TransactionStatusCode.Should().Be(TopUpTransactionStatusCodes.Completed);
        second.TransactionStatusCode.Should().Be(TopUpTransactionStatusCodes.Pending);
        third.TransactionStatusCode.Should().Be(TopUpTransactionStatusCodes.Completed);
    }

    private PendingTransactionRecoveryService CreateService()
    {
        return new PendingTransactionRecoveryService(
            _transactions,
            _runs,
            _accountGateway,
            _events,
            _metrics,
            _unitOfWork,
            _clock,
            NullLogger<PendingTransactionRecoveryService>.Instance);
    }

    private static TopUpTransaction Pending(long runId, long accountId, decimal amount)
        => TopUpTransaction.Create(runId, accountId, amount, DateTime.UtcNow);

    private sealed class FakeTopUpTransactionRepository : ITopUpTransactionRepository
    {
        private readonly List<TopUpTransaction> _transactions = [];

        public void Seed(params TopUpTransaction[] transactions) => _transactions.AddRange(transactions);
        public Task<TopUpTransaction?> GetByIdAsync(long id, CancellationToken cancellationToken = default) => Task.FromResult<TopUpTransaction?>(null);
        public Task<TopUpTransaction?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default) => Task.FromResult(_transactions.SingleOrDefault(x => x.IdempotencyKey == idempotencyKey));
        public Task<TopUpTransaction?> GetByRunAndAccountAsync(long topUpRunId, long educationAccountId, CancellationToken cancellationToken = default) => Task.FromResult(_transactions.SingleOrDefault(x => x.TopUpRunId == topUpRunId && x.EducationAccountId == educationAccountId));
        public Task<List<TopUpTransaction>> GetByRunIdAsync(long topUpRunId, CancellationToken cancellationToken = default) => Task.FromResult(_transactions.Where(x => x.TopUpRunId == topUpRunId).ToList());
        public Task<IReadOnlyList<TopUpTransaction>> GetPendingByRunIdPagedAsync(long topUpRunId, int skip, int take, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<TopUpTransaction>>(_transactions.Where(x => x.TopUpRunId == topUpRunId && x.TransactionStatusCode == TopUpTransactionStatusCodes.Pending).Skip(skip).Take(take).ToList());
        public Task<decimal> GetTotalDisbursedForCampaignAsync(long campaignId, CancellationToken cancellationToken = default) => Task.FromResult(0m);
        public Task<List<TopUpTransaction>> GetByAccountIdAsync(long educationAccountId, CancellationToken cancellationToken = default) => Task.FromResult(_transactions.Where(x => x.EducationAccountId == educationAccountId).ToList());
        public Task<(List<TopUpTransaction> Transactions, long TotalCount)> GetByAccountIdPagedAsync(long educationAccountId, int skip, int take, CancellationToken cancellationToken = default)
        {
            var all = _transactions.Where(x => x.EducationAccountId == educationAccountId).OrderByDescending(x => x.CompletedAtUtc ?? x.CreatedAtUtc).ToList();
            return Task.FromResult<(List<TopUpTransaction>, long)>((all.Skip(skip).Take(take).ToList(), all.Count));
        }
        public Task<long> CountByAccountIdAsync(long educationAccountId, CancellationToken cancellationToken = default) => Task.FromResult((long)_transactions.Count(x => x.EducationAccountId == educationAccountId));
        public void Add(TopUpTransaction transaction) => _transactions.Add(transaction);
        public Task AddAsync(TopUpTransaction transaction, CancellationToken cancellationToken = default) { Add(transaction); return Task.CompletedTask; }
    }

    private sealed class FakeAccountCreditGateway : IAccountCreditGateway
    {
        private readonly Queue<Func<Result<CreditAccountResult>>> _results = new();
        public int Calls { get; private set; }

        public void EnqueueSuccess(long accountTransactionId, bool alreadyProcessed)
            => _results.Enqueue(() => Result<CreditAccountResult>.Success(new CreditAccountResult { AccountTransactionId = accountTransactionId, AlreadyProcessed = alreadyProcessed }));

        public void EnqueueFailure(Error error)
            => _results.Enqueue(() => Result<CreditAccountResult>.Failure(error));

        public void EnqueueException()
            => _results.Enqueue(() => throw new InvalidOperationException("Recovery failed"));

        public Task<Result<CreditAccountResult>> CreditAccountForTopUpAsync(long educationAccountId, decimal amount, string idempotencyKey, string reason, CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(_results.Dequeue().Invoke());
        }
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(1);
    }

    private sealed class FakeClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow { get; } = utcNow;
    }

    private sealed class FakeTopUpExecutionEventPublisher : ITopUpExecutionEventPublisher
    {
        public List<TopUpReceivedReport> TopUpReceivedReports { get; } = [];

        public Task PublishRunStartedAsync(
            TopUpRunStartedReport report,
            CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task PublishRunCompletedAsync(
            TopUpRunCompletedReport report,
            CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task PublishTopUpReceivedAsync(
            TopUpReceivedReport report,
            CancellationToken cancellationToken = default)
        {
            TopUpReceivedReports.Add(report);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeTopUpRunRepository : ITopUpRunRepository
    {
        private TopUpRun? _run;

        public void Seed(TopUpRun run) => _run = run;
        public Task<TopUpRun?> GetByIdAsync(long id, CancellationToken cancellationToken = default) => Task.FromResult(_run);
        public Task<IReadOnlyList<TopUpRun>> GetByIdsAsync(IReadOnlyList<long> ids, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<TopUpRun>>(_run != null && ids.Contains(_run.Id) ? [_run] : []);
        public Task<TopUpRun?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default) => Task.FromResult<TopUpRun?>(null);
        public Task<bool> ExistsForScheduledOccurrenceAsync(long campaignId, DateTime scheduledFor, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<bool> HasRunsForCampaignAsync(long campaignId, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<bool> HasActiveRunsForCampaignAsync(long campaignId, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task AddAsync(TopUpRun run, CancellationToken cancellationToken = default) { _run = run; return Task.CompletedTask; }
    }

    private sealed class FakeTopUpExecutionMetrics : ITopUpExecutionMetrics
    {
        public void RecordRunStarted(long topUpRunId, long campaignId, int totalSelected) { }

        public void RecordRunCompleted(
            long topUpRunId,
            long campaignId,
            string terminalStatus,
            int totalProcessed,
            int totalSucceeded,
            int totalFailed,
            int totalSkipped,
            TimeSpan duration)
        { }

        public void RecordRecipientProcessed(
            long topUpRunId,
            string status,
            bool duplicateIdempotencyHit,
            bool accountCreditFailure)
        { }

        public void RecordAccountCreditDbConflict() { }
    }
}
