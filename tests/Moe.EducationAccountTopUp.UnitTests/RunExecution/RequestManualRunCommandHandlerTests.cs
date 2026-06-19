using System.Reflection;
using FluentAssertions;
using Moe.Application.Abstractions.Clock;
using Moe.Application.Abstractions.Security;
using Moe.Modules.EducationAccountTopUp.Application.RunExecution.RequestManualRun;
using Moe.Modules.EducationAccountTopUp.Domain.TopUps;
using Moe.Modules.EducationAccountTopUp.IGateway;
using Moe.Modules.EducationAccountTopUp.IGateway.Repositories;
using Xunit;

namespace Moe.EducationAccountTopUp.UnitTests.RunExecution;

public sealed class RequestManualRunCommandHandlerTests
{
    private readonly FakeTopUpCampaignRepository _campaigns = new();
    private readonly FakeTopUpRunRepository _runs = new();
    private readonly FakeTopUpRunDispatcher _dispatcher = new();
    private readonly FakeCurrentUser _currentUser = new();
    private readonly FakeClock _clock = new(new DateTimeOffset(2026, 6, 17, 4, 0, 0, TimeSpan.Zero));

    [Fact]
    public async Task Should_Create_Run_When_Campaign_Active_And_Key_Unique()
    {
        var campaign = TopUpCampaign.Create(1, "CAMPAIGN-01", "Test", null, "FIXED", 100m, "Reason", "IMMEDIATE", new DateOnly(2026, 1, 1), null, null, null, 99, DateTime.UtcNow);
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
        var campaign = TopUpCampaign.Create(1, "CAMPAIGN-01", "Test", null, "FIXED", 100m, "Reason", "IMMEDIATE", new DateOnly(2026, 1, 1), null, null, null, 99, DateTime.UtcNow);
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
    public async Task Should_Fail_When_User_Lacks_Permission()
    {
        _currentUser.AllowTopUpsManage = false;

        RequestManualRunCommandHandler handler = CreateHandler();
        var result = await handler.Handle(new RequestManualRunCommand(10, "manual-key-1", null), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(TopUpErrors.Unauthorized);
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
        var campaign = TopUpCampaign.Create(1, "CAMPAIGN-01", "Test", null, "FIXED", 100m, "Reason", "IMMEDIATE", new DateOnly(2026, 1, 1), null, null, null, 99, DateTime.UtcNow);
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
        var campaign = TopUpCampaign.Create(1, "CAMPAIGN-01", "Test", null, "FIXED", 100m, "Reason", "IMMEDIATE", new DateOnly(2026, 1, 1), null, null, null, 99, DateTime.UtcNow);
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
        var campaign = TopUpCampaign.Create(1, "CAMPAIGN-01", "Test", null, "FIXED", 100m, "Reason", "IMMEDIATE", new DateOnly(2026, 1, 1), null, null, null, 99, DateTime.UtcNow);
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

    private RequestManualRunCommandHandler CreateHandler()
    {
        return new RequestManualRunCommandHandler(
            _campaigns,
            _runs,
            _dispatcher,
            _currentUser,
            _clock);
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

        public Task<IReadOnlyList<Moe.Modules.EducationAccountTopUp.Application.TopUps.GetCampaigns.CampaignListItem>> ListAsync(
            IReadOnlyCollection<long> accessibleOrgIds,
            CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
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
        public IReadOnlyCollection<string> Roles { get; } = ["SYSTEM_ADMIN"];
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
