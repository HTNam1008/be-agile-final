using System.Reflection;
using FluentAssertions;
using Moe.Application.Abstractions.Clock;
using Moe.Application.Abstractions.Security;
using Moe.Modules.EducationAccountTopUp.Application.RunExecution.RequestManualRun;
using Moe.Modules.EducationAccountTopUp.Domain.TopUps;
using Moe.Modules.EducationAccountTopUp.IGateway;
using Moe.Modules.EducationAccountTopUp.IGateway.Repositories;
using Moe.SharedKernel.Results;
using Xunit;

namespace Moe.EducationAccountTopUp.UnitTests.RunExecution;

public sealed class RequestManualRunCommandHandlerTests
{
    private readonly FakeTopUpCampaignRepository _campaigns = new();
    private readonly FakeTopUpRunRepository _runs = new();
    private readonly FakeTopUpRunDispatcher _dispatcher = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeCurrentUser _currentUser = new();
    private readonly FakeClock _clock = new(new DateTimeOffset(2026, 6, 17, 4, 0, 0, TimeSpan.Zero));

    [Fact]
    public async Task Should_Create_Run_When_Campaign_Active_And_Key_Unique()
    {
        var campaign = TopUpCampaign.Create(1, "CAMPAIGN-01", "Test", null, "FIXED", 100m, "Reason", "IMMEDIATE", new DateOnly(2026, 1, 1), null, null, null, "INSTANT", 100m, 99, DateTime.UtcNow);
        typeof(Moe.SharedKernel.Domain.Entity<long>).GetProperty("Id")!.SetValue(campaign, 10);
        campaign.ChangeStatus(TopUpCampaignStatusCodes.Active, 99, DateTime.UtcNow);
        _campaigns.Add(campaign);

        RequestManualRunCommandHandler handler = CreateHandler();
        var result = await handler.Handle(new RequestManualRunCommand(10, "manual-key-1", "Backfill"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.RunId.Should().Be(1);
        result.Value.Status.Should().Be(TopUpRunStatusCodes.Previewed);
        result.Value.IdempotencyKey.Should().Be("manual-key-1");
        result.Value.RequestedAtUtc.Should().Be(_clock.UtcNow.UtcDateTime);
        _runs.AddCalls.Should().Be(1);
    }

    [Fact]
    public async Task Should_Return_Existing_Run_When_Idempotency_Key_Duplicate()
    {
        var campaign = TopUpCampaign.Create(1, "CAMPAIGN-01", "Test", null, "FIXED", 100m, "Reason", "IMMEDIATE", new DateOnly(2026, 1, 1), null, null, null, "INSTANT", 100m, 99, DateTime.UtcNow);
        typeof(Moe.SharedKernel.Domain.Entity<long>).GetProperty("Id")!.SetValue(campaign, 10);
        campaign.ChangeStatus(TopUpCampaignStatusCodes.Active, 99, DateTime.UtcNow);
        _campaigns.Add(campaign);
        var existingRun = TopUpRun.Rehydrate(
            42,
            10,
            1,
            _clock.UtcNow.UtcDateTime,
            TopUpRunTriggerTypes.Manual,
            99,
            TopUpRunStatusCodes.Previewed,
            "manual-key-1");
        _runs.Seed(existingRun);

        RequestManualRunCommandHandler handler = CreateHandler();
        var result = await handler.Handle(new RequestManualRunCommand(10, "manual-key-1", "Retry"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.RunId.Should().Be(42);
        _runs.AddCalls.Should().Be(0);
        _dispatcher.EnqueueCalls.Should().Be(0);
    }

    [Fact]
    public async Task Should_Fail_When_Campaign_Is_Outside_Admin_Scope()
    {
        var campaign = TopUpCampaign.Create(2, "CAMPAIGN-02", "Test", null, "FIXED", 100m, "Reason", "IMMEDIATE", new DateOnly(2026, 1, 1), null, null, null, "INSTANT", 100m, 99, DateTime.UtcNow);
        typeof(Moe.SharedKernel.Domain.Entity<long>).GetProperty("Id")!.SetValue(campaign, 10);
        campaign.ChangeStatus(TopUpCampaignStatusCodes.Active, 99, DateTime.UtcNow);
        _campaigns.Add(campaign);

        RequestManualRunCommandHandler handler = CreateHandler(new ScopedAdminAccess([1]));
        var result = await handler.Handle(new RequestManualRunCommand(10, "manual-key-1", null), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("AUTH.ORGANIZATION_OUTSIDE_SCOPE");
        _runs.AddCalls.Should().Be(0);
    }

    [Fact]
    public async Task Should_Fail_When_Campaign_Not_Found()
    {
        RequestManualRunCommandHandler handler = CreateHandler();
        var result = await handler.Handle(new RequestManualRunCommand(404, "manual-key-1", null), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(TopUpErrors.CampaignNotFound);
        _runs.AddCalls.Should().Be(0);
    }

    [Theory]
    [InlineData(TopUpCampaignStatusCodes.Draft)]
    [InlineData(TopUpCampaignStatusCodes.Paused)]
    [InlineData(TopUpCampaignStatusCodes.Cancelled)]
    public async Task Should_Fail_When_Campaign_Not_Active(string statusCode)
    {
        var campaign = TopUpCampaign.Create(1, "CAMPAIGN-01", "Test", null, "FIXED", 100m, "Reason", "IMMEDIATE", new DateOnly(2026, 1, 1), null, null, null, "INSTANT", 100m, 99, DateTime.UtcNow);
        typeof(Moe.SharedKernel.Domain.Entity<long>).GetProperty("Id")!.SetValue(campaign, 10);
        campaign.ChangeStatus(statusCode, 99, DateTime.UtcNow);
        _campaigns.Add(campaign);

        RequestManualRunCommandHandler handler = CreateHandler();
        var result = await handler.Handle(new RequestManualRunCommand(10, "manual-key-1", null), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(TopUpErrors.CampaignNotExecutable);
        _runs.AddCalls.Should().Be(0);
    }

    [Fact]
    public async Task Should_Enqueue_Run_For_Background_Processing()
    {
        var campaign = TopUpCampaign.Create(1, "CAMPAIGN-01", "Test", null, "FIXED", 100m, "Reason", "IMMEDIATE", new DateOnly(2026, 1, 1), null, null, null, "INSTANT", 100m, 99, DateTime.UtcNow);
        typeof(Moe.SharedKernel.Domain.Entity<long>).GetProperty("Id")!.SetValue(campaign, 10);
        campaign.ChangeStatus(TopUpCampaignStatusCodes.Active, 99, DateTime.UtcNow);
        _campaigns.Add(campaign);

        RequestManualRunCommandHandler handler = CreateHandler();
        var result = await handler.Handle(new RequestManualRunCommand(10, "manual-key-1", null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _dispatcher.EnqueueCalls.Should().Be(1);
        _dispatcher.EnqueuedRunIds.Should().ContainSingle().Which.Should().Be(result.Value.RunId);
    }

    [Fact]
    public async Task Should_Raise_ManualRunRequestedEvent()
    {
        var campaign = TopUpCampaign.Create(1, "CAMPAIGN-01", "Test", null, "FIXED", 100m, "Reason", "IMMEDIATE", new DateOnly(2026, 1, 1), null, null, null, "INSTANT", 100m, 99, DateTime.UtcNow);
        typeof(Moe.SharedKernel.Domain.Entity<long>).GetProperty("Id")!.SetValue(campaign, 10);
        campaign.ChangeStatus(TopUpCampaignStatusCodes.Active, 99, DateTime.UtcNow);
        _campaigns.Add(campaign);

        RequestManualRunCommandHandler handler = CreateHandler();
        var result = await handler.Handle(new RequestManualRunCommand(10, "manual-key-1", null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        TopUpRun run = _runs.Items.Should().ContainSingle().Subject;
        ManualRunRequestedEvent domainEvent = run.DomainEvents
            .OfType<ManualRunRequestedEvent>()
            .Should()
            .ContainSingle()
            .Subject;
        domainEvent.TopUpRunId.Should().Be(result.Value.RunId);
        domainEvent.CampaignId.Should().Be(10);
        domainEvent.RequestedByUserId.Should().Be(_currentUser.UserAccountId);
    }

    [Fact]
    public async Task Should_Fail_When_Campaign_Is_DynamicRules()
    {
        var campaign = TopUpCampaign.Create(1, "CAMPAIGN-01", "Test", null, "DYNAMIC_RULES", 100m, "Reason", "Recurring", new DateOnly(2026, 1, 1), null, "Quarterly", 1, "CONDITIONAL_RECURRING", 500m, 99, DateTime.UtcNow);
        typeof(Moe.SharedKernel.Domain.Entity<long>).GetProperty("Id")!.SetValue(campaign, 10);
        campaign.ChangeStatus(TopUpCampaignStatusCodes.Active, 99, DateTime.UtcNow);
        _campaigns.Add(campaign);

        RequestManualRunCommandHandler handler = CreateHandler();
        var result = await handler.Handle(new RequestManualRunCommand(10, "manual-key-1", null), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(TopUpErrors.ManualRunDisabled);
        _runs.AddCalls.Should().Be(0);
    }

    private RequestManualRunCommandHandler CreateHandler(IAdminAccessControl? adminAccess = null)
    {
        return new RequestManualRunCommandHandler(
            _campaigns,
            _runs,
            _unitOfWork,
            _dispatcher,
            _currentUser,
            adminAccess ?? new AllowAllAdminAccess(),
            _clock);
    }

    private sealed class FakeUnitOfWork : Moe.Application.Abstractions.Persistence.IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(1);
        }
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

    private sealed class ScopedAdminAccess(IReadOnlyCollection<long> organizationIds) : IAdminAccessControl
    {
        public bool IsHqAdmin => false;
        public bool IsSchoolAdmin => true;
        public IReadOnlyCollection<long> ScopedOrganizationIds => organizationIds;
        public bool CanAccessOrganization(long organizationId) => organizationIds.Contains(organizationId);
        public Result EnsureCanAccessOrganization(long organizationId)
            => CanAccessOrganization(organizationId)
                ? Result.Success()
                : Result.Failure(new Error("AUTH.ORGANIZATION_OUTSIDE_SCOPE", "The requested organization is outside the current admin's scope."));
        public AdminOrganizationScope ResolveOrganizationFilter(long? requestedOrganizationId)
            => new(requestedOrganizationId is null || CanAccessOrganization(requestedOrganizationId.Value), false, requestedOrganizationId, organizationIds);
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

        public Task<bool> CampaignCodeExistsAsync(
            long organizationId,
            string campaignCode,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_campaigns.Values.Any(x => x.OrganizationId == organizationId
                && x.CampaignCode == campaignCode));
        }



        public Task<IReadOnlyList<TopUpCampaign>> GetDueCampaignsAsync(DateTime utcNow, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<TopUpCampaign>>(_campaigns.Values
                .Where(c => c.CampaignStatusCode == TopUpCampaignStatusCodes.Active && c.NextRunAtUtc != null && c.NextRunAtUtc <= utcNow)
                .ToList());
        }

        public Task<IReadOnlyList<TopUpCampaignRule>> GetRulesAsync(
            long campaignId,
            CancellationToken cancellationToken = default)
        {
            IReadOnlyList<TopUpCampaignRule> rules = [];
            return Task.FromResult(rules);
        }

        public Task<IReadOnlyList<TopUpCampaignRecipient>> GetActiveRecipientsAsync(
            long campaignId,
            CancellationToken cancellationToken = default)
        {
            IReadOnlyList<TopUpCampaignRecipient> recipients = [];
            return Task.FromResult(recipients);
        }

        public Task RemoveRulesAsync(IEnumerable<TopUpCampaignRule> rules, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task AddRuleAsync(TopUpCampaignRule rule, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IReadOnlyList<TopUpCampaignRecipient>> GetRecipientsAsync(long campaignId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<TopUpCampaignRecipient>>([]);
        public Task<Dictionary<long, decimal>> GetAmountOverridesByCampaignAsync(long campaignId, CancellationToken cancellationToken = default) => Task.FromResult(new Dictionary<long, decimal>());
        public Task RemoveRecipientsAsync(IEnumerable<TopUpCampaignRecipient> recipients, long userId, DateTime nowUtc, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task AddRecipientAsync(TopUpCampaignRecipient recipient, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<int> CountActiveRulesAsync(long campaignId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(0);
        }

        public Task<int> CountActiveRecipientsAsync(long campaignId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(0);
        }

        public Task<IReadOnlyList<TopUpCampaign>> GetDueForAssessmentAsync(DateOnly today, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<TopUpCampaign>>([]);
        }

        public Task AddAsync(TopUpCampaign campaign, CancellationToken cancellationToken = default)
        {
            Add(campaign);
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
    private sealed class FakeTopUpRunRepository : ITopUpRunRepository
    {
        private static readonly PropertyInfo IdProperty =
            typeof(TopUpRun).GetProperty(nameof(TopUpRun.Id))!;

        private long _nextId = 1;
        private readonly List<TopUpRun> _runs = [];

        public int AddCalls { get; private set; }
        public IReadOnlyCollection<TopUpRun> Items => _runs.AsReadOnly();

        public void Seed(TopUpRun run) => _runs.Add(run);

        public Task<TopUpRun?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_runs.SingleOrDefault(x => x.Id == id));
        }

        public Task<TopUpRun?> GetByIdempotencyKeyAsync(
            string idempotencyKey,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_runs.SingleOrDefault(x => x.IdempotencyKey == idempotencyKey));
        }

        public Task<bool> ExistsForScheduledOccurrenceAsync(
            long campaignId,
            DateTime scheduledFor,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_runs.Any(x => x.TopUpCampaignId == campaignId && x.ScheduledForUtc == scheduledFor));
        }

        public Task<bool> HasRunsForCampaignAsync(long campaignId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_runs.Any(x => x.TopUpCampaignId == campaignId && x.RunStatusCode != TopUpRunStatusCodes.Failed));
        }

        public Task<bool> HasActiveRunsForCampaignAsync(long campaignId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(false);
        }

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

    private sealed class FakeCurrentUser : ICurrentUser
    {
        public bool AllowTopUpsManage { get; set; } = true;
        public long? UserAccountId { get; init; } = 99;
        public long? PersonId => null;
        public long? OrganizationUnitId => 1;
        public IReadOnlyCollection<long> OrganizationUnitIds { get; } = [1];
        public IReadOnlyCollection<string> Roles { get; } = ["HQ_ADMIN"];
        public IReadOnlyCollection<string> Permissions => AllowTopUpsManage ? ["TOPUPS_MANAGE"] : [];
        public string Portal => "ADMIN";
        public bool IsAuthenticated => true;
        public bool HasPermission(string permission) => Permissions.Contains(permission);
    }

    private sealed class FakeClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow { get; } = utcNow;
    }

}
