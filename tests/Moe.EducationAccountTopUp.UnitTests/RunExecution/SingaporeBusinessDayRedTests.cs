using System.Reflection;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moe.Application.Abstractions.Clock;
using Moe.Application.Abstractions.Persistence;
using Moe.Modules.EducationAccountTopUp.Application.RunExecution;
using Moe.Modules.EducationAccountTopUp.Application.TopUps.GetCampaigns;
using Moe.Modules.EducationAccountTopUp.Application.TopUps.PreviewCampaign;
using Moe.Modules.EducationAccountTopUp.Domain.TopUps;
using Moe.Modules.EducationAccountTopUp.IGateway.Repositories;
using Moe.Modules.EducationAccountTopUp.IGateway.TopUps;
using Xunit;

namespace Moe.EducationAccountTopUp.UnitTests.RunExecution;

public sealed class SingaporeBusinessDayRedTests
{
    private static readonly DateTimeOffset SgtEarlyMorning =
        new(2026, 6, 30, 16, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task TopUpAssessmentWorker_uses_singapore_business_day_for_due_assessment()
    {
        FakeTopUpCampaignRepository campaigns = new();
        ServiceProvider services = new ServiceCollection()
            .AddSingleton<ITopUpCampaignRepository>(campaigns)
            .AddSingleton<ITopUpCampaignReader>(new EmptyCampaignReader())
            .AddSingleton<IDynamicRuleFilter>(new EmptyRuleFilter())
            .AddSingleton<IDynamicTopUpContractRepository>(new EmptyContractRepository())
            .AddSingleton<IUnitOfWork>(new NoopUnitOfWork())
            .AddSingleton<IDistributedLock>(new AlwaysAcquiredLock())
            .BuildServiceProvider();
        TopUpAssessmentWorker worker = new(
            services.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<TopUpAssessmentWorker>.Instance,
            new TestClock(SgtEarlyMorning),
            Options.Create(new TopUpWorkerOptions()));

        MethodInfo run = typeof(TopUpAssessmentWorker).GetMethod(
            "RunAssessmentAsync",
            BindingFlags.Instance | BindingFlags.NonPublic)!;

        await (Task)run.Invoke(worker, [CancellationToken.None])!;

        campaigns.ObservedDueDate.Should().Be(new DateOnly(2026, 7, 1));
    }

    private sealed class FakeTopUpCampaignRepository : ITopUpCampaignRepository
    {
        public DateOnly? ObservedDueDate { get; private set; }
        public Task<IReadOnlyList<TopUpCampaign>> GetDueForAssessmentAsync(DateOnly today, CancellationToken cancellationToken = default)
        {
            ObservedDueDate = today;
            return Task.FromResult<IReadOnlyList<TopUpCampaign>>([]);
        }

        public Task<TopUpCampaign?> GetByIdAsync(long id, CancellationToken cancellationToken = default) => Task.FromResult<TopUpCampaign?>(null);
        public Task<IReadOnlyList<TopUpCampaign>> GetByIdsAsync(IReadOnlyList<long> ids, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<TopUpCampaign>>([]);
        public Task<bool> CampaignCodeExistsAsync(long organizationId, string campaignCode, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<IReadOnlyList<TopUpCampaign>> GetDueCampaignsAsync(DateTime utcNow, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<TopUpCampaign>>([]);
        public Task<int> CountActiveRulesAsync(long campaignId, CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task<int> CountActiveRecipientsAsync(long campaignId, CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task<IReadOnlyList<TopUpCampaignRule>> GetRulesAsync(long campaignId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<TopUpCampaignRule>>([]);
        public Task RemoveRulesAsync(IEnumerable<TopUpCampaignRule> rules, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task AddRuleAsync(TopUpCampaignRule rule, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IReadOnlyList<TopUpCampaignRecipient>> GetRecipientsAsync(long campaignId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<TopUpCampaignRecipient>>([]);
        public Task<Dictionary<long, decimal>> GetAmountOverridesByCampaignAsync(long campaignId, CancellationToken cancellationToken = default) => Task.FromResult(new Dictionary<long, decimal>());
        public Task RemoveRecipientsAsync(IEnumerable<TopUpCampaignRecipient> recipients, long userId, DateTime nowUtc, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task AddRecipientAsync(TopUpCampaignRecipient recipient, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task AddAsync(TopUpCampaign campaign, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class EmptyCampaignReader : ITopUpCampaignReader
    {
        public Task<CampaignListItem?> GetByIdAsync(long id, CancellationToken cancellationToken = default) => Task.FromResult<CampaignListItem?>(null);
        public Task<CampaignListResult> GetCampaignsAsync(IReadOnlyCollection<long>? accessibleOrgIds, int pageNumber = 1, int pageSize = 50, string? search = null, string? status = null, DateOnly? dateFrom = null, DateOnly? dateTo = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<CampaignRuleProjection>> GetRulesAsync(long campaignId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<CampaignRuleProjection>>([]);
        public Task<IReadOnlyList<ActiveRecipientProjection>> GetActiveRecipientsAsync(long campaignId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ActiveRecipientProjection>>([]);
        public Task<(int TotalCount, IReadOnlyList<PreviewFixedRecipient> Items)> GetFixedRecipientsForPreviewAsync(long campaignId, int skip, int take, CancellationToken cancellationToken = default) => Task.FromResult<(int, IReadOnlyList<PreviewFixedRecipient>)>((0, []));
        public Task<CampaignPreviewSummary?> GetPreviewSummaryAsync(long campaignId, CancellationToken cancellationToken = default) => Task.FromResult<CampaignPreviewSummary?>(null);
    }

    private sealed class EmptyRuleFilter : IDynamicRuleFilter
    {
        public Task<int> CountMatchingAccountsAsync(IReadOnlyList<CampaignRuleProjection> rules, DateTime nowUtc, CancellationToken ct) => Task.FromResult(0);
        public Task<IReadOnlyList<long>> FilterAccountIdsAsync(IReadOnlyList<CampaignRuleProjection> rules, int skip, int take, DateTime nowUtc, CancellationToken ct) => Task.FromResult<IReadOnlyList<long>>([]);
    }

    private sealed class EmptyContractRepository : IDynamicTopUpContractRepository
    {
        public Task<DynamicTopUpContract?> GetByCampaignAndAccountAsync(long campaignId, long accountId, CancellationToken cancellationToken = default) => Task.FromResult<DynamicTopUpContract?>(null);
        public Task<IReadOnlyList<DynamicTopUpContract>> GetActiveByCampaignAsync(long campaignId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<DynamicTopUpContract>>([]);
        public Task<IReadOnlyList<DynamicTopUpContract>> GetDueForPaymentAsync(DateTime nowUtc, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<DynamicTopUpContract>>([]);
        public Task<IReadOnlyList<DynamicTopUpContract>> GetActiveByCampaignAndAccountsAsync(long campaignId, IEnumerable<long> accountIds, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<DynamicTopUpContract>>([]);
        public Task<IReadOnlyList<DynamicTopUpContract>> GetByCampaignAndAccountsAsync(long campaignId, IEnumerable<long> accountIds, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<DynamicTopUpContract>>([]);
        public Task<IReadOnlyList<DynamicTopUpContract>> GetByAccountIdAsync(long accountId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<DynamicTopUpContract>>([]);
        public Task AddAsync(DynamicTopUpContract contract, CancellationToken ct) => Task.CompletedTask;
        public Task SuspendNonQualifyingContractsAsync(long campaignId, IEnumerable<long> qualifyingAccountIds, DateTime suspendedAtUtc, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task ShiftContractPaymentDatesAsync(long campaignId, TimeSpan pauseDuration, DateTime nowUtc, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task CancelAllActiveContractsAsync(long campaignId, DateTime cancelledAtUtc, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task CancelNonFixedContractActiveContractsAsync(long campaignId, DateTime completedAtUtc, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class AlwaysAcquiredLock : IDistributedLock
    {
        public Task<bool> TryAcquireAsync(string key, TimeSpan expiry, CancellationToken ct = default) => Task.FromResult(true);
        public Task ReleaseAsync(string key) => Task.CompletedTask;
    }

    private sealed class NoopUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);
    }

    private sealed class TestClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow { get; } = utcNow;

        public DateOnly TodayInSingapore() => SingaporeBusinessDay.FromUtc(UtcNow);
    }
}
