using System.Reflection;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moe.Application.Abstractions.Clock;
using Moe.Application.Abstractions.Persistence;
using Moe.Modules.EducationAccountTopUp.Application.RunExecution;
using Moe.Modules.EducationAccountTopUp.Domain.TopUps;
using Moe.Modules.EducationAccountTopUp.IGateway.Repositories;
using Xunit;

namespace Moe.EducationAccountTopUp.UnitTests.RunExecution;

public sealed class RunReconciliationServiceTests
{
    private readonly FakeTopUpCampaignRepository _campaigns = new();
    private readonly FakeTopUpRunRepository _runs = new();
    private readonly FakeTopUpTransactionRepository _transactions = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeClock _clock = new(new DateTimeOffset(2026, 6, 18, 4, 0, 0, TimeSpan.Zero));

    [Fact]
    public async Task Should_Finalize_Processing_Run_With_Reconciled_Totals()
    {
        TopUpRun run = AddProcessingRun();
        _transactions.Seed(Completed(run.Id, 1, 100m), Completed(run.Id, 2, 100m), Completed(run.Id, 3, 100m));

        var result = await CreateService().ReconcileRunAsync(run.Id);

        result.IsSuccess.Should().BeTrue();
        result.Value.ReconciliationStatus.Should().Be("Finalized");
        result.Value.RunStatus.Should().Be(TopUpRunStatusCodes.Completed);
        run.TotalSucceeded.Should().Be(3);
        run.TotalAmount.Should().Be(300m);
        _unitOfWork.SaveCalls.Should().Be(1);
    }

    [Fact]
    public async Task Should_Calculate_Partial_Totals_Correctly()
    {
        TopUpRun run = AddProcessingRun();
        _transactions.Seed(
            Completed(run.Id, 1, 100m),
            Completed(run.Id, 2, 100m),
            Failed(run.Id, 3, 50m),
            Skipped(run.Id, 4, 50m));

        var result = await CreateService().ReconcileRunAsync(run.Id);

        result.IsSuccess.Should().BeTrue();
        result.Value.RunStatus.Should().Be(TopUpRunStatusCodes.Partial);
        result.Value.Summary.TotalAmount.Should().Be(200m);
        result.Value.Summary.TotalSucceeded.Should().Be(2);
        result.Value.Summary.TotalFailed.Should().Be(1);
        result.Value.Summary.TotalSkipped.Should().Be(1);
    }

    [Fact]
    public async Task Should_Exclude_Failed_Skipped_From_Total_Amount()
    {
        TopUpRun run = AddProcessingRun();
        _transactions.Seed(Completed(run.Id, 1, 500m), Failed(run.Id, 2, 200m), Failed(run.Id, 3, 200m));

        var result = await CreateService().ReconcileRunAsync(run.Id);

        result.IsSuccess.Should().BeTrue();
        result.Value.Summary.TotalAmount.Should().Be(500m);
    }

    [Fact]
    public async Task Should_Return_Incomplete_When_Pending_Transactions_Exist()
    {
        TopUpRun run = AddProcessingRun();
        _transactions.Seed(Pending(run.Id, 1, 100m), Completed(run.Id, 2, 100m));

        var result = await CreateService().ReconcileRunAsync(run.Id);

        result.IsSuccess.Should().BeTrue();
        result.Value.ReconciliationStatus.Should().Be("Incomplete");
        result.Value.Summary.TotalPending.Should().Be(1);
        run.RunStatusCode.Should().Be(TopUpRunStatusCodes.Processing);
        _unitOfWork.SaveCalls.Should().Be(0);
    }

    [Fact]
    public async Task Should_Verify_Matching_Totals_On_Terminal_Run()
    {
        TopUpRun run = AddCompletedRun(totalSucceeded: 2, totalFailed: 0, totalSkipped: 0, totalAmount: 200m);
        _transactions.Seed(Completed(run.Id, 1, 100m), Completed(run.Id, 2, 100m));

        var result = await CreateService().ReconcileRunAsync(run.Id);

        result.IsSuccess.Should().BeTrue();
        result.Value.ReconciliationStatus.Should().Be("Verified");
    }

    [Fact]
    public async Task Should_Detect_Mismatch_On_Terminal_Run()
    {
        TopUpRun run = AddCompletedRun(totalSucceeded: 1, totalFailed: 0, totalSkipped: 0, totalAmount: 100m);
        _transactions.Seed(Completed(run.Id, 1, 100m), Completed(run.Id, 2, 100m));

        var result = await CreateService().ReconcileRunAsync(run.Id);

        result.IsSuccess.Should().BeTrue();
        result.Value.ReconciliationStatus.Should().Be("Mismatch");
        result.Value.MismatchDetails.Should().Contain("TotalSucceeded");
        result.Value.MismatchDetails.Should().Contain("TotalAmount");
    }

    [Fact]
    public async Task Should_Return_Error_When_Run_Not_Found()
    {
        var result = await CreateService().ReconcileRunAsync(999);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(TopUpErrors.RunNotFound);
    }

    [Fact]
    public async Task Should_Handle_Empty_Transaction_List()
    {
        TopUpRun run = AddProcessingRun();

        var result = await CreateService().ReconcileRunAsync(run.Id);

        result.IsSuccess.Should().BeTrue();
        result.Value.ReconciliationStatus.Should().Be("Finalized");
        result.Value.RunStatus.Should().Be(TopUpRunStatusCodes.Failed);
        result.Value.Summary.TotalProcessed.Should().Be(0);
    }

    [Fact]
    public async Task Should_Calculate_All_Failed_As_Failed_Status()
    {
        TopUpRun run = AddProcessingRun();
        _transactions.Seed(Failed(run.Id, 1, 100m), Failed(run.Id, 2, 100m), Failed(run.Id, 3, 100m));

        var result = await CreateService().ReconcileRunAsync(run.Id);

        result.IsSuccess.Should().BeTrue();
        result.Value.RunStatus.Should().Be(TopUpRunStatusCodes.Failed);
        result.Value.Summary.TotalFailed.Should().Be(3);
    }

    [Fact]
    public async Task Should_Sum_Only_Completed_Transaction_Amounts()
    {
        TopUpRun run = AddProcessingRun();
        _transactions.Seed(Completed(run.Id, 1, 100m), Completed(run.Id, 2, 200m), Failed(run.Id, 3, 300m));

        var result = await CreateService().ReconcileRunAsync(run.Id);

        result.IsSuccess.Should().BeTrue();
        result.Value.Summary.TotalAmount.Should().Be(300m);
    }

    private RunReconciliationService CreateService()
    {
        return new RunReconciliationService(
            _campaigns,
            new FakeDynamicTopUpContractRepository(),
            _runs,
            _transactions,
            _unitOfWork,
            _clock,
            NullLogger<RunReconciliationService>.Instance);
    }

    private TopUpRun AddProcessingRun()
    {
        TopUpRun run = CreateRun();
        run.StartProcessing(_clock.UtcNow.UtcDateTime).IsSuccess.Should().BeTrue();
        run.ClearDomainEvents();
        _runs.Add(run);
        return run;
    }

    private TopUpRun AddCompletedRun(int totalSucceeded, int totalFailed, int totalSkipped, decimal totalAmount)
    {
        TopUpRun run = AddProcessingRun();
        int totalProcessed = totalSucceeded + totalFailed + totalSkipped;
        run.Finalize(
            totalProcessed,
            totalSucceeded,
            totalFailed,
            totalSkipped,
            totalAmount,
            _clock.UtcNow.UtcDateTime).IsSuccess.Should().BeTrue();
        run.ClearDomainEvents();
        return run;
    }

    private TopUpRun CreateRun()
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
            "INSTANT",
            100m,
            99,
            _clock.UtcNow.UtcDateTime);
        campaign.ChangeStatus(TopUpCampaignStatusCodes.Active, 99, _clock.UtcNow.UtcDateTime);

        return TopUpRun.CreateManual(
            campaign,
            $"run-key-{Guid.NewGuid():N}",
            99,
            _clock.UtcNow.UtcDateTime,
            null);
    }

    private static TopUpTransaction Pending(long runId, long accountId, decimal amount)
        => TopUpTransaction.Create(runId, accountId, amount, DateTime.UtcNow);

    private static TopUpTransaction Completed(long runId, long accountId, decimal amount)
    {
        TopUpTransaction transaction = Pending(runId, accountId, amount);
        transaction.Complete(1000 + accountId, DateTime.UtcNow).IsSuccess.Should().BeTrue();
        return transaction;
    }

    private static TopUpTransaction Failed(long runId, long accountId, decimal amount)
    {
        TopUpTransaction transaction = Pending(runId, accountId, amount);
        transaction.Fail(SafeReasons.CreditRejected, DateTime.UtcNow).IsSuccess.Should().BeTrue();
        return transaction;
    }

    private static TopUpTransaction Skipped(long runId, long accountId, decimal amount)
    {
        TopUpTransaction transaction = Pending(runId, accountId, amount);
        transaction.Skip(SafeReasons.AccountClosed, DateTime.UtcNow).IsSuccess.Should().BeTrue();
        return transaction;
    }

    private sealed class FakeTopUpRunRepository : ITopUpRunRepository
    {
        private static readonly PropertyInfo IdProperty =
            typeof(TopUpRun).GetProperty(nameof(TopUpRun.Id))!;

        private long _nextId = 1;
        private readonly Dictionary<long, TopUpRun> _runs = [];

        public void Add(TopUpRun run)
        {
            if (run.Id == 0)
            {
                IdProperty.SetValue(run, _nextId++);
            }

            _runs[run.Id] = run;
        }

        public Task<TopUpRun?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
        {
            _runs.TryGetValue(id, out TopUpRun? run);
            return Task.FromResult(run);
        }

        public Task<IReadOnlyList<TopUpRun>> GetByIdsAsync(IReadOnlyList<long> ids, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<TopUpRun>>(_runs.Values.Where(r => ids.Contains(r.Id)).ToList());
        }

        public Task<TopUpRun?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default)
            => Task.FromResult(_runs.Values.SingleOrDefault(x => x.IdempotencyKey == idempotencyKey));

        public Task<bool> ExistsForScheduledOccurrenceAsync(long campaignId, DateTime scheduledFor, CancellationToken cancellationToken = default)
            => Task.FromResult(_runs.Values.Any(x => x.TopUpCampaignId == campaignId && x.ScheduledForUtc == scheduledFor));

        public Task<bool> HasRunsForCampaignAsync(long campaignId, CancellationToken cancellationToken = default)
            => Task.FromResult(_runs.Values.Any(x => x.TopUpCampaignId == campaignId && x.RunStatusCode != TopUpRunStatusCodes.Failed));

        public Task<bool> HasActiveRunsForCampaignAsync(long campaignId, CancellationToken cancellationToken = default)
            => Task.FromResult(_runs.Values.Any(x => x.TopUpCampaignId == campaignId && (x.RunStatusCode == TopUpRunStatusCodes.Previewed || x.RunStatusCode == TopUpRunStatusCodes.Processing)));

        public Task AddAsync(TopUpRun run, CancellationToken cancellationToken = default)
        {
            Add(run);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeTopUpTransactionRepository : ITopUpTransactionRepository
    {
        private readonly List<TopUpTransaction> _transactions = [];

        public void Seed(params TopUpTransaction[] transactions) => _transactions.AddRange(transactions);

        public Task<TopUpTransaction?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
            => Task.FromResult<TopUpTransaction?>(null);

        public Task<TopUpTransaction?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default)
            => Task.FromResult(_transactions.SingleOrDefault(x => x.IdempotencyKey == idempotencyKey));

        public Task<TopUpTransaction?> GetByRunAndAccountAsync(long topUpRunId, long educationAccountId, CancellationToken cancellationToken = default)
            => Task.FromResult(_transactions.SingleOrDefault(x => x.TopUpRunId == topUpRunId && x.EducationAccountId == educationAccountId));

        public Task<List<TopUpTransaction>> GetByRunIdAsync(long topUpRunId, CancellationToken cancellationToken = default)
            => Task.FromResult(_transactions.Where(x => x.TopUpRunId == topUpRunId).ToList());

        public Task<IReadOnlyList<TopUpTransaction>> GetPendingByRunIdPagedAsync(long topUpRunId, int skip, int take, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<TopUpTransaction>>(_transactions.Where(x => x.TopUpRunId == topUpRunId && x.TransactionStatusCode == TopUpTransactionStatusCodes.Pending).Skip(skip).Take(take).ToList());

        public Task<decimal> GetTotalDisbursedForCampaignAsync(long campaignId, CancellationToken cancellationToken = default)
            => Task.FromResult(0m);

        public Task<List<TopUpTransaction>> GetByAccountIdAsync(long educationAccountId, CancellationToken cancellationToken = default)
            => Task.FromResult(_transactions.Where(x => x.EducationAccountId == educationAccountId).ToList());

        public Task<(List<TopUpTransaction> Transactions, long TotalCount)> GetByAccountIdPagedAsync(long educationAccountId, int skip, int take, CancellationToken cancellationToken = default)
        {
            var all = _transactions.Where(x => x.EducationAccountId == educationAccountId).OrderByDescending(x => x.CompletedAtUtc ?? x.CreatedAtUtc).ToList();
            return Task.FromResult<(List<TopUpTransaction>, long)>((all.Skip(skip).Take(take).ToList(), all.Count));
        }

        public Task<long> CountByAccountIdAsync(long educationAccountId, CancellationToken cancellationToken = default)
            => Task.FromResult((long)_transactions.Count(x => x.EducationAccountId == educationAccountId));

        public void Add(TopUpTransaction transaction) => _transactions.Add(transaction);

        public Task AddAsync(TopUpTransaction transaction, CancellationToken cancellationToken = default)
        {
            Add(transaction);
            return Task.CompletedTask;
        }

        public Task<bool> TryReserveBudgetAsync(long campaignId, decimal requestedAmount, decimal budgetCap, CancellationToken cancellationToken = default)
            => Task.FromResult(true);
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

    private sealed class FakeTopUpCampaignRepository : ITopUpCampaignRepository
    {
        public Task<TopUpCampaign?> GetByIdAsync(long id, CancellationToken cancellationToken = default) => Task.FromResult<TopUpCampaign?>(null);
        public Task<IReadOnlyList<TopUpCampaign>> GetByIdsAsync(IReadOnlyList<long> ids, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<TopUpCampaign>>([]);
        public Task<TopUpCampaign?> GetByCodeAsync(string campaignCode, CancellationToken cancellationToken = default) => Task.FromResult<TopUpCampaign?>(null);
        public Task<bool> CampaignCodeExistsAsync(long organizationId, string campaignCode, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<IReadOnlyList<TopUpCampaign>> GetDueCampaignsAsync(DateTime utcNow, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<TopUpCampaign>>([]);
        public Task<IReadOnlyList<TopUpCampaign>> GetDueForAssessmentAsync(DateOnly date, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<TopUpCampaign>>([]);
        public Task<int> CountActiveRulesAsync(long campaignId, CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task<int> CountActiveRecipientsAsync(long campaignId, CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task<IReadOnlyList<TopUpCampaignRule>> GetRulesAsync(long campaignId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<TopUpCampaignRule>>([]);
        public Task DeleteRuleGroupsByCampaignIdAsync(long campaignId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task AddRuleGroupAsync(TopUpRuleGroup group, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IReadOnlyList<TopUpCampaignRecipient>> GetRecipientsAsync(long campaignId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<TopUpCampaignRecipient>>([]);
        public Task<Dictionary<long, decimal>> GetAmountOverridesByCampaignAsync(long campaignId, CancellationToken cancellationToken = default) => Task.FromResult(new Dictionary<long, decimal>());
        public Task RemoveRecipientsAsync(IEnumerable<TopUpCampaignRecipient> recipients, long userId, DateTime deletedAtUtc, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task AddRecipientAsync(TopUpCampaignRecipient recipient, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task AddAsync(TopUpCampaign campaign, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow { get; } = utcNow;

        public DateOnly TodayInSingapore() => SingaporeBusinessDay.FromUtc(UtcNow);
    }

    private sealed class FakeDynamicTopUpContractRepository : IDynamicTopUpContractRepository
    {
        public Task<DynamicTopUpContract?> GetByCampaignAndAccountAsync(long campaignId, long accountId, CancellationToken cancellationToken = default) => Task.FromResult<DynamicTopUpContract?>(null);
        public Task<IReadOnlyList<DynamicTopUpContract>> GetActiveByCampaignAsync(long campaignId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<DynamicTopUpContract>>([]);
        public Task<IReadOnlyList<DynamicTopUpContract>> GetDueForPaymentAsync(DateTime nowUtc, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<DynamicTopUpContract>>([]);
        public Task AddAsync(DynamicTopUpContract contract, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IReadOnlyList<DynamicTopUpContract>> GetActiveByCampaignAndAccountsAsync(long campaignId, IEnumerable<long> accountIds, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<DynamicTopUpContract>>([]);
        public Task<IReadOnlyList<DynamicTopUpContract>> GetByCampaignAndAccountsAsync(long campaignId, IEnumerable<long> accountIds, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<DynamicTopUpContract>>([]);
        public Task SuspendNonQualifyingContractsAsync(long campaignId, IEnumerable<long> qualifyingAccountIds, DateTime suspendedAtUtc, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IReadOnlyList<DynamicTopUpContract>> GetByAccountIdAsync(long accountId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<DynamicTopUpContract>>([]);
        public Task ShiftContractPaymentDatesAsync(long campaignId, TimeSpan pauseDuration, DateTime nowUtc, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task CancelAllActiveContractsAsync(long campaignId, DateTime cancelledAtUtc, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task CancelNonFixedContractActiveContractsAsync(long campaignId, DateTime completedAtUtc, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
