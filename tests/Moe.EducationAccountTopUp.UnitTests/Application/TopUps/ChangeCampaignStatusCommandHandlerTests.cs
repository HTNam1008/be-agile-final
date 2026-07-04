using System.Reflection;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moe.Application.Abstractions.Audit;
using Moe.Application.Abstractions.Clock;
using Moe.Application.Abstractions.Persistence;
using Moe.Application.Abstractions.Security;
using Moe.Modules.EducationAccountTopUp.Application.TopUps.ChangeCampaignStatus;
using Moe.Modules.EducationAccountTopUp.Contracts.TopUps.Enums;
using Moe.Modules.EducationAccountTopUp.Domain.TopUps;
using Moe.Modules.EducationAccountTopUp.IGateway;
using Moe.Modules.EducationAccountTopUp.IGateway.Repositories;
using Moe.Modules.Notifications.IGateway.Notifications;
using Moe.SharedKernel.Results;
using Xunit;

namespace Moe.EducationAccountTopUp.UnitTests.Application.TopUps;

public sealed class ChangeCampaignStatusCommandHandlerTests
{
    private readonly FakeTopUpCampaignRepository _campaigns = new();
    private readonly FakeDynamicTopUpContractRepository _contracts = new();
    private readonly FakeTopUpRunRepository _runs = new();
    private readonly FakeTopUpRunDispatcher _dispatcher = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeCurrentUser _currentUser = new();
    private readonly FakeClock _clock = new(new DateTimeOffset(2026, 7, 3, 2, 30, 0, TimeSpan.Zero));

    [Fact]
    public async Task Activate_DynamicInstantCampaign_MakesLiveWithoutCreatingRun()
    {
        TopUpCampaign campaign = CreateCampaign(
            recipientModeCode: RecipientModeCode.DynamicRules.ToString(),
            scheduleTypeCode: ScheduleTypeCode.Immediate.ToString(),
            deliveryTypeCode: DeliveryType.Instant);
        _campaigns.Add(campaign);
        _campaigns.ActiveRuleCount = 1;

        ChangeCampaignStatusCommandHandler handler = CreateHandler();
        Result result = await handler.Handle(new ChangeCampaignStatusCommand(campaign.Id, TopUpCampaignStatusCodes.Active), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        campaign.CampaignStatusCode.Should().Be(TopUpCampaignStatusCodes.Active);
        campaign.NextRunAtUtc.Should().BeNull();
        _runs.AddCalls.Should().Be(0);
        _dispatcher.EnqueueCalls.Should().Be(0);
    }

    [Fact]
    public async Task Activate_FixedImmediateInstantCampaign_StillCreatesImmediateRun()
    {
        TopUpCampaign campaign = CreateCampaign(
            recipientModeCode: RecipientModeCode.FixedSelection.ToString(),
            scheduleTypeCode: ScheduleTypeCode.Immediate.ToString(),
            deliveryTypeCode: DeliveryType.Instant);
        _campaigns.Add(campaign);
        _campaigns.ActiveRecipientCount = 1;

        ChangeCampaignStatusCommandHandler handler = CreateHandler();
        Result result = await handler.Handle(new ChangeCampaignStatusCommand(campaign.Id, TopUpCampaignStatusCodes.Active), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        campaign.CampaignStatusCode.Should().Be(TopUpCampaignStatusCodes.Active);
        campaign.NextRunAtUtc.Should().BeNull();
        _runs.AddCalls.Should().Be(1);
        _dispatcher.EnqueueCalls.Should().Be(1);
        _dispatcher.EnqueuedRunIds.Should().ContainSingle().Which.Should().Be(_runs.Items.Single().Id);
    }

    [Fact]
    public async Task DueCampaignLookup_ExcludesDynamicCampaignsEvenIfNextRunIsDue()
    {
        DateTime now = _clock.UtcNow.UtcDateTime;
        TopUpCampaign dynamicCampaign = CreateCampaign(
            recipientModeCode: RecipientModeCode.DynamicRules.ToString(),
            scheduleTypeCode: ScheduleTypeCode.Immediate.ToString(),
            deliveryTypeCode: DeliveryType.Instant,
            id: 11);
        dynamicCampaign.SetNextRunAt(now.AddMinutes(-1));
        dynamicCampaign.ChangeStatus(TopUpCampaignStatusCodes.Active, _currentUser.UserAccountId!.Value, now).IsSuccess.Should().BeTrue();

        TopUpCampaign fixedCampaign = CreateCampaign(
            recipientModeCode: RecipientModeCode.FixedSelection.ToString(),
            scheduleTypeCode: ScheduleTypeCode.OneTimeScheduled.ToString(),
            deliveryTypeCode: DeliveryType.Instant,
            id: 12);
        fixedCampaign.SetNextRunAt(now.AddMinutes(-1));
        fixedCampaign.ChangeStatus(TopUpCampaignStatusCodes.Active, _currentUser.UserAccountId!.Value, now).IsSuccess.Should().BeTrue();

        _campaigns.Add(dynamicCampaign);
        _campaigns.Add(fixedCampaign);

        IReadOnlyList<TopUpCampaign> due = await _campaigns.GetDueCampaignsAsync(now);

        due.Should().ContainSingle().Which.Id.Should().Be(fixedCampaign.Id);
    }

    private ChangeCampaignStatusCommandHandler CreateHandler()
        => new(
            _campaigns,
            _contracts,
            _runs,
            _dispatcher,
            _unitOfWork,
            _currentUser,
            new AllowAllAdminAccess(),
            _clock,
            new NoopAuditService(),
            new NoopNotificationWriter(),
            NullLogger<ChangeCampaignStatusCommandHandler>.Instance);

    private static TopUpCampaign CreateCampaign(
        string recipientModeCode,
        string scheduleTypeCode,
        string deliveryTypeCode,
        long id = 10)
    {
        TopUpCampaign campaign = TopUpCampaign.Create(
            organizationId: 1,
            campaignCode: $"CAMPAIGN-{id}",
            campaignName: "Test campaign",
            description: null,
            recipientModeCode: recipientModeCode,
            defaultTopUpAmount: 100m,
            reason: "Test top-up",
            scheduleTypeCode: scheduleTypeCode,
            startDate: new DateOnly(2026, 7, 3),
            endDate: scheduleTypeCode == ScheduleTypeCode.Recurring.ToString() ? new DateOnly(2026, 12, 31) : null,
            frequencyCode: scheduleTypeCode == ScheduleTypeCode.Recurring.ToString() ? FrequencyCode.Monthly.ToString() : null,
            frequencyInterval: scheduleTypeCode == ScheduleTypeCode.Recurring.ToString() ? 1 : null,
            deliveryTypeCode: deliveryTypeCode,
            maxTotalAmount: deliveryTypeCode == DeliveryType.Instant ? 100m : 500m,
            currentUserId: 99,
            nowUtc: new DateTime(2026, 7, 3, 2, 30, 0, DateTimeKind.Utc));

        typeof(TopUpCampaign)
            .BaseType!
            .GetProperty(nameof(TopUpCampaign.Id), BindingFlags.Instance | BindingFlags.Public)!
            .SetValue(campaign, id);

        return campaign;
    }

    private sealed class FakeTopUpCampaignRepository : ITopUpCampaignRepository
    {
        private readonly Dictionary<long, TopUpCampaign> _campaigns = [];

        public int ActiveRuleCount { get; set; }
        public int ActiveRecipientCount { get; set; }

        public void Add(TopUpCampaign campaign) => _campaigns[campaign.Id] = campaign;

        public Task<TopUpCampaign?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
            => Task.FromResult(_campaigns.GetValueOrDefault(id));

        public Task<IReadOnlyList<TopUpCampaign>> GetByIdsAsync(IReadOnlyList<long> ids, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<TopUpCampaign>>(_campaigns.Values.Where(c => ids.Contains(c.Id)).ToList());

        public Task<bool> CampaignCodeExistsAsync(long organizationId, string campaignCode, CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task<IReadOnlyList<TopUpCampaign>> GetDueCampaignsAsync(DateTime utcNow, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<TopUpCampaign>>(_campaigns.Values
                .Where(c => c.CampaignStatusCode == TopUpCampaignStatusCodes.Active
                    && c.RecipientModeCode != RecipientModeCode.DynamicRules.ToString()
                    && c.NextRunAtUtc is not null
                    && c.NextRunAtUtc <= utcNow)
                .ToList());

        public Task<IReadOnlyList<TopUpCampaign>> GetDueForAssessmentAsync(DateOnly today, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<TopUpCampaign>>([]);

        public Task<int> CountActiveRulesAsync(long campaignId, CancellationToken cancellationToken = default)
            => Task.FromResult(ActiveRuleCount);

        public Task<int> CountActiveRecipientsAsync(long campaignId, CancellationToken cancellationToken = default)
            => Task.FromResult(ActiveRecipientCount);

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
        public int ShiftCalls { get; private set; }
        public int CancelCalls { get; private set; }

        public Task<DynamicTopUpContract?> GetByCampaignAndAccountAsync(long campaignId, long accountId, CancellationToken cancellationToken = default) => Task.FromResult<DynamicTopUpContract?>(null);
        public Task<IReadOnlyList<DynamicTopUpContract>> GetActiveByCampaignAsync(long campaignId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<DynamicTopUpContract>>([]);
        public Task<IReadOnlyList<DynamicTopUpContract>> GetDueForPaymentAsync(DateTime nowUtc, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<DynamicTopUpContract>>([]);
        public Task AddAsync(DynamicTopUpContract contract, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IReadOnlyList<DynamicTopUpContract>> GetActiveByCampaignAndAccountsAsync(long campaignId, IEnumerable<long> accountIds, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<DynamicTopUpContract>>([]);
        public Task<IReadOnlyList<DynamicTopUpContract>> GetByCampaignAndAccountsAsync(long campaignId, IEnumerable<long> accountIds, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<DynamicTopUpContract>>([]);
        public Task SuspendNonQualifyingContractsAsync(long campaignId, IEnumerable<long> qualifyingAccountIds, DateTime suspendedAtUtc, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IReadOnlyList<DynamicTopUpContract>> GetByAccountIdAsync(long accountId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<DynamicTopUpContract>>([]);
        public Task ShiftContractPaymentDatesAsync(long campaignId, TimeSpan pauseDuration, DateTime nowUtc, CancellationToken cancellationToken = default) { ShiftCalls++; return Task.CompletedTask; }
        public Task CancelAllActiveContractsAsync(long campaignId, DateTime cancelledAtUtc, CancellationToken cancellationToken = default) { CancelCalls++; return Task.CompletedTask; }
        public Task CancelNonFixedContractActiveContractsAsync(long campaignId, DateTime completedAtUtc, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeTopUpRunRepository : ITopUpRunRepository
    {
        private static readonly PropertyInfo IdProperty = typeof(TopUpRun).GetProperty(nameof(TopUpRun.Id))!;
        private readonly List<TopUpRun> _runs = [];
        private long _nextId = 1;

        public int AddCalls { get; private set; }
        public IReadOnlyCollection<TopUpRun> Items => _runs.AsReadOnly();

        public Task<TopUpRun?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
            => Task.FromResult(_runs.SingleOrDefault(x => x.Id == id));

        public Task<IReadOnlyList<TopUpRun>> GetByIdsAsync(IReadOnlyList<long> ids, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<TopUpRun>>(_runs.Where(r => ids.Contains(r.Id)).ToList());

        public Task<TopUpRun?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default)
            => Task.FromResult(_runs.SingleOrDefault(x => x.IdempotencyKey == idempotencyKey));

        public Task<bool> ExistsForScheduledOccurrenceAsync(long campaignId, DateTime scheduledFor, CancellationToken cancellationToken = default)
            => Task.FromResult(_runs.Any(x => x.TopUpCampaignId == campaignId && x.ScheduledForUtc == scheduledFor));

        public Task<bool> HasRunsForCampaignAsync(long campaignId, CancellationToken cancellationToken = default)
            => Task.FromResult(_runs.Any(x => x.TopUpCampaignId == campaignId));

        public Task<bool> HasActiveRunsForCampaignAsync(long campaignId, CancellationToken cancellationToken = default)
            => Task.FromResult(_runs.Any(x => x.TopUpCampaignId == campaignId && !TopUpRunStatusCodes.TerminalStatuses.Contains(x.RunStatusCode)));

        public Task AddAsync(TopUpRun run, CancellationToken cancellationToken = default)
        {
            AddCalls++;
            IdProperty.SetValue(run, _nextId++);
            _runs.Add(run);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeTopUpRunDispatcher : ITopUpRunDispatcher
    {
        private readonly List<long> _enqueuedRunIds = [];
        public int EnqueueCalls { get; private set; }
        public IReadOnlyCollection<long> EnqueuedRunIds => _enqueuedRunIds.AsReadOnly();

        public Task EnqueueAsync(long topUpRunId, CancellationToken cancellationToken = default)
        {
            EnqueueCalls++;
            _enqueuedRunIds.Add(topUpRunId);
            return Task.CompletedTask;
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

    private sealed class FakeCurrentUser : ICurrentUser
    {
        public long? UserAccountId => 99;
        public long? PersonId => null;
        public long? OrganizationUnitId => 1;
        public IReadOnlyCollection<long> OrganizationUnitIds { get; } = [1];
        public IReadOnlyCollection<string> Roles { get; } = ["HQ_ADMIN"];
        public IReadOnlyCollection<string> Permissions { get; } = ["TOPUPS_MANAGE"];
        public string Portal => "ADMIN";
        public bool IsAuthenticated => true;
        public bool HasPermission(string permission) => Permissions.Contains(permission);
    }

    private sealed class FakeClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow { get; } = utcNow;
        public DateOnly TodayInSingapore() => SingaporeBusinessDay.FromUtc(UtcNow);
    }

    private sealed class AllowAllAdminAccess : IAdminAccessControl
    {
        public bool IsHqAdmin => true;
        public bool IsSchoolAdmin => false;
        public IReadOnlyCollection<long> ScopedOrganizationIds => [];
        public bool CanAccessOrganization(long organizationId) => true;
        public Result EnsureCanAccessOrganization(long organizationId) => Result.Success();
        public AdminOrganizationScope ResolveOrganizationFilter(long? requestedOrganizationId)
            => new(true, true, requestedOrganizationId, []);
    }

    private sealed class NoopAuditService : IAuditService
    {
        public Task RecordAsync(string actionCode, string entityTypeCode, string entityId, string? detailsJson = null, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task RecordSchoolActionAsync(SchoolAuditContext context, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class NoopNotificationWriter : INotificationWriter
    {
        public Task<Result<long>> CreateAsync(NotificationCreateRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(Result<long>.Success(1));
    }
}
