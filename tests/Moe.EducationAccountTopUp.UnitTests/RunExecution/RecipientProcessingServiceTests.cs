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

public sealed class RecipientProcessingServiceTests
{
    private readonly FakeTopUpTransactionRepository _transactions = new();
    private readonly FakeAccountCreditGateway _accountCreditGateway = new();
    private readonly FakeRecipientValidator _recipientValidator = new();
    private readonly FakeTopUpExecutionEventPublisher _events = new();
    private readonly FakeTopUpExecutionMetrics _metrics = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeClock _clock = new(new DateTimeOffset(2026, 6, 17, 4, 0, 0, TimeSpan.Zero));

    [Fact]
    public async Task Should_Process_Recipient_Successfully()
    {
        RecipientProcessingService service = CreateService();

        var result = await service.ProcessRecipientAsync(42, 100, 500m, 1, "Campaign top-up");

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(TopUpTransactionStatusCodes.Completed);
        result.Value.AccountTransactionId.Should().Be(1001);
        result.Value.CreditedAmount.Should().Be(500m);

        TopUpTransaction transaction = _transactions.Items.Should().ContainSingle().Subject;
        transaction.TransactionStatusCode.Should().Be(TopUpTransactionStatusCodes.Completed);
        transaction.AccountTransactionId.Should().Be(1001);
        transaction.Amount.Should().Be(500m);
    }

    [Fact]
    public async Task Should_Return_Existing_When_Already_Completed()
    {
        TopUpTransaction existing = CreateStoredTransaction(42, 100, 500m);
        existing.Complete(777, _clock.UtcNow.UtcDateTime).IsSuccess.Should().BeTrue();
        _transactions.Seed(existing);

        RecipientProcessingService service = CreateService();
        var result = await service.ProcessRecipientAsync(42, 100, 500m, 1, "Campaign top-up");

        result.IsSuccess.Should().BeTrue();
        result.Value.TopUpTransactionId.Should().Be(existing.Id);
        result.Value.AccountTransactionId.Should().Be(777);
        result.Value.AlreadyProcessed.Should().BeTrue();
        _accountCreditGateway.Calls.Should().Be(0);
        _unitOfWork.SaveCalls.Should().Be(0);
    }

    [Fact]
    public async Task Should_Skip_When_Recipient_Not_Eligible()
    {
        _recipientValidator.Result = Result.Failure(TopUpErrors.RecipientNotEligible);
        RecipientProcessingService service = CreateService();

        var result = await service.ProcessRecipientAsync(42, 100, 500m, 1, "Campaign top-up");

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(TopUpTransactionStatusCodes.Skipped);
        result.Value.Reason.Should().Be(TopUpErrors.RecipientNotEligible.Message);
        _transactions.Items.Single().Amount.Should().Be(500m);
    }

    [Fact]
    public async Task Should_Fail_When_Credit_Service_Returns_Error()
    {
        Error creditError = new("Account.CreditFailed", "Credit rejected");
        _accountCreditGateway.Result = Result<CreditAccountResult>.Failure(creditError);
        RecipientProcessingService service = CreateService();

        var result = await service.ProcessRecipientAsync(42, 100, 500m, 1, "Campaign top-up");

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(TopUpTransactionStatusCodes.Failed);
        result.Value.Reason.Should().Be("Credit rejected");
        _transactions.Items.Single().Amount.Should().Be(500m);
    }

    [Fact]
    public async Task Should_Fail_When_Credit_Service_Throws_Exception()
    {
        _accountCreditGateway.ThrowOnCredit = true;
        RecipientProcessingService service = CreateService();

        var result = await service.ProcessRecipientAsync(42, 100, 500m, 1, "Campaign top-up");

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(TopUpTransactionStatusCodes.Failed);
        result.Value.Reason.Should().Be("Credit service unavailable");
        _transactions.Items.Single().Reason.Should().Be("Credit service unavailable");
    }

    [Fact]
    public async Task Should_Handle_Already_Processed_Credit()
    {
        _accountCreditGateway.Result = Result<CreditAccountResult>.Success(new CreditAccountResult
        {
            AccountTransactionId = 888,
            AlreadyProcessed = true
        });
        RecipientProcessingService service = CreateService();

        var result = await service.ProcessRecipientAsync(42, 100, 500m, 1, "Campaign top-up");

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(TopUpTransactionStatusCodes.Completed);
        result.Value.AccountTransactionId.Should().Be(888);
        result.Value.AlreadyProcessed.Should().BeTrue();
        _transactions.Items.Single().AccountTransactionId.Should().Be(888);
    }

    [Fact]
    public async Task Should_Recover_Pending_Transaction_On_Retry()
    {
        TopUpTransaction existing = CreateStoredTransaction(42, 100, 500m);
        _transactions.Seed(existing);

        RecipientProcessingService service = CreateService();
        var result = await service.ProcessRecipientAsync(42, 100, 500m, 1, "Campaign top-up");

        result.IsSuccess.Should().BeTrue();
        result.Value.TopUpTransactionId.Should().Be(existing.Id);
        result.Value.Status.Should().Be(TopUpTransactionStatusCodes.Completed);
        _transactions.AddCalls.Should().Be(0);
        _accountCreditGateway.Calls.Should().Be(1);
    }

    [Fact]
    public async Task Should_Use_Correct_Idempotency_Key()
    {
        RecipientProcessingService service = CreateService();

        await service.ProcessRecipientAsync(42, 100, 500m, 1, "Campaign top-up");

        _accountCreditGateway.LastIdempotencyKey.Should().Be("topup:42:100");
    }

    [Fact]
    public async Task Should_Not_Call_Credit_When_Validation_Fails()
    {
        _recipientValidator.Result = Result.Failure(TopUpErrors.RecipientNotEligible);
        RecipientProcessingService service = CreateService();

        await service.ProcessRecipientAsync(42, 100, 500m, 1, "Campaign top-up");

        _accountCreditGateway.Calls.Should().Be(0);
    }

    [Fact]
    public async Task Should_Persist_Pending_Before_Credit_Call()
    {
        _accountCreditGateway.OnCredit = () => _unitOfWork.SaveCalls.Should().Be(1);
        RecipientProcessingService service = CreateService();

        await service.ProcessRecipientAsync(42, 100, 500m, 1, "Campaign top-up");

        _accountCreditGateway.Calls.Should().Be(1);
        _unitOfWork.SaveCalls.Should().Be(2);
    }

    [Fact]
    public async Task Should_Emit_TopUpReceived_After_Completed_Credit_Is_Committed()
    {
        RecipientProcessingService service = CreateService();
        _events.OnTopUpReceived = () => _unitOfWork.SaveCalls.Should().Be(2);

        var result = await service.ProcessRecipientAsync(42, 100, 500m, 1, "Campaign top-up");

        result.IsSuccess.Should().BeTrue();
        _events.TopUpReceivedReports.Should().ContainSingle();
        _events.TopUpReceivedReports.Single().TopUpTransactionId.Should().Be(_transactions.Items.Single().Id);
        _events.TopUpReceivedReports.Single().AccountTransactionId.Should().Be(1001);
    }

    [Fact]
    public async Task Should_Record_Recipient_Metrics()
    {
        RecipientProcessingService service = CreateService();

        await service.ProcessRecipientAsync(42, 100, 500m, 1, "Campaign top-up");

        _metrics.RecipientRecords.Should().ContainSingle();
        _metrics.RecipientRecords.Single().Status.Should().Be(TopUpTransactionStatusCodes.Completed);
    }

    private RecipientProcessingService CreateService()
    {
        return new RecipientProcessingService(
            _transactions,
            _accountCreditGateway,
            _recipientValidator,
            _events,
            _metrics,
            _unitOfWork,
            _clock,
            NullLogger<RecipientProcessingService>.Instance);
    }

    private static TopUpTransaction CreateStoredTransaction(long runId, long accountId, decimal amount)
    {
        TopUpTransaction transaction = TopUpTransaction.Create(
            runId,
            accountId,
            amount,
            new DateTime(2026, 6, 17, 4, 0, 0, DateTimeKind.Utc));

        FakeTopUpTransactionRepository.SetId(transaction, 123);
        return transaction;
    }

    private sealed class FakeTopUpTransactionRepository : ITopUpTransactionRepository
    {
        private static readonly PropertyInfo IdProperty =
            typeof(TopUpTransaction).GetProperty(nameof(TopUpTransaction.Id))!;

        private long _nextId = 1;
        private readonly List<TopUpTransaction> _transactions = [];

        public int AddCalls { get; private set; }
        public IReadOnlyCollection<TopUpTransaction> Items => _transactions.AsReadOnly();

        public static void SetId(TopUpTransaction transaction, long id)
        {
            IdProperty.SetValue(transaction, id);
        }

        public void Seed(TopUpTransaction transaction) => _transactions.Add(transaction);

        public Task<TopUpTransaction?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_transactions.SingleOrDefault(x => x.Id == id));
        }

        public Task<TopUpTransaction?> GetByIdempotencyKeyAsync(
            string idempotencyKey,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_transactions.SingleOrDefault(x => x.IdempotencyKey == idempotencyKey));
        }

        public Task<TopUpTransaction?> GetByRunAndAccountAsync(
            long topUpRunId,
            long educationAccountId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_transactions.SingleOrDefault(
                x => x.TopUpRunId == topUpRunId && x.EducationAccountId == educationAccountId));
        }

        public Task<List<TopUpTransaction>> GetByRunIdAsync(
            long topUpRunId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_transactions.Where(x => x.TopUpRunId == topUpRunId).ToList());
        }

        public Task<IReadOnlyList<TopUpTransaction>> GetPendingByRunIdPagedAsync(
            long topUpRunId,
            int skip,
            int take,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<TopUpTransaction>>(
                _transactions.Where(x => x.TopUpRunId == topUpRunId && x.TransactionStatusCode == TopUpTransactionStatusCodes.Pending)
                    .Skip(skip)
                    .Take(take)
                    .ToList());
        }

        public void Add(TopUpTransaction transaction)
        {
            AddCalls++;
            SetId(transaction, _nextId++);
            _transactions.Add(transaction);
        }

        public Task AddAsync(TopUpTransaction transaction, CancellationToken cancellationToken = default)
        {
            Add(transaction);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeAccountCreditGateway : IAccountCreditGateway
    {
        public int Calls { get; private set; }
        public string? LastIdempotencyKey { get; private set; }
        public bool ThrowOnCredit { get; set; }
        public Action? OnCredit { get; set; }
        public Result<CreditAccountResult> Result { get; set; } =
            Result<CreditAccountResult>.Success(new CreditAccountResult
            {
                AccountTransactionId = 1001,
                AlreadyProcessed = false
            });

        public Task<Result<CreditAccountResult>> CreditAccountForTopUpAsync(
            long educationAccountId,
            decimal amount,
            string idempotencyKey,
            string reason,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            LastIdempotencyKey = idempotencyKey;
            OnCredit?.Invoke();

            if (ThrowOnCredit)
            {
                throw new InvalidOperationException("Gateway unavailable");
            }

            return Task.FromResult(Result);
        }
    }

    private sealed class FakeRecipientValidator : IRecipientValidator
    {
        public Result Result { get; set; } = Result.Success();

        public Task<Result> ValidateRecipientAsync(
            long educationAccountId,
            long organizationUnitId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Result);
        }
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

    private sealed class FakeClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow { get; } = utcNow;
    }

    private sealed class FakeTopUpExecutionEventPublisher : ITopUpExecutionEventPublisher
    {
        public List<TopUpReceivedReport> TopUpReceivedReports { get; } = [];
        public Action? OnTopUpReceived { get; set; }

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
            OnTopUpReceived?.Invoke();
            TopUpReceivedReports.Add(report);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeTopUpExecutionMetrics : ITopUpExecutionMetrics
    {
        public List<(long TopUpRunId, string Status, bool Duplicate, bool AccountCreditFailure)> RecipientRecords { get; } = [];

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
            => RecipientRecords.Add((topUpRunId, status, duplicateIdempotencyHit, accountCreditFailure));

        public void RecordAccountCreditDbConflict() { }
    }
}
