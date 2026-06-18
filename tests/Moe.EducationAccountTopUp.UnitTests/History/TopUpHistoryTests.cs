using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moe.Application.Abstractions.Persistence;
using Moe.Application.Abstractions.Security;
using Moe.Infrastructure.Shared.Api;
using Moe.Modules.EducationAccountTopUp;
using Moe.Modules.EducationAccountTopUp.Application.History;
using Moe.Modules.EducationAccountTopUp.Application.History.CampaignHistory;
using Moe.Modules.EducationAccountTopUp.Application.History.RunHistory;
using Moe.Modules.EducationAccountTopUp.Contracts.TopUps;
using Moe.Modules.EducationAccountTopUp.Domain.EducationAccounts;
using Moe.Modules.EducationAccountTopUp.Domain.TopUps;
using Moe.Modules.EducationAccountTopUp.Infrastructure.History;
using Moe.Modules.IdentityPlatform.Domain.People;
using Moe.StudentFinance.Persistence;
using Xunit;

namespace Moe.EducationAccountTopUp.UnitTests.History;

public sealed class TopUpHistoryTests : IAsyncLifetime
{
    private readonly MoeDbContext _dbContext;
    private readonly TopUpHistoryReader _reader;

    private TopUpCampaign _newerScopedCampaign = null!;
    private TopUpCampaign _olderScopedCampaign = null!;
    private TopUpCampaign _outsideScopeCampaign = null!;
    private TopUpRun _completedRun = null!;
    private TopUpRun _outsideScopeRun = null!;

    public TopUpHistoryTests()
    {
        DbContextOptions<MoeDbContext> options = new DbContextOptionsBuilder<MoeDbContext>()
            .UseInMemoryDatabase($"top-up-history-{Guid.NewGuid():N}")
            .Options;

        _dbContext = new MoeDbContext(
            options,
            [new HistoryTestModelConfiguration()]);

        _reader = new TopUpHistoryReader(_dbContext);
    }

    public async Task InitializeAsync()
    {
        await _dbContext.Database.EnsureCreatedAsync();

        _olderScopedCampaign = CreateCampaign(
            organizationId: 1,
            code: "HISTORY-OLD",
            name: "Older scoped campaign",
            actorId: 101,
            createdAtUtc: new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc));

        _newerScopedCampaign = CreateCampaign(
            organizationId: 1,
            code: "HISTORY-NEW",
            name: "Newer scoped campaign",
            actorId: 102,
            createdAtUtc: new DateTime(2026, 6, 10, 0, 0, 0, DateTimeKind.Utc));

        _outsideScopeCampaign = CreateCampaign(
            organizationId: 2,
            code: "HISTORY-OUTSIDE",
            name: "Outside scope campaign",
            actorId: 103,
            createdAtUtc: new DateTime(2026, 6, 12, 0, 0, 0, DateTimeKind.Utc));

        _dbContext.AddRange(
            _olderScopedCampaign,
            _newerScopedCampaign,
            _outsideScopeCampaign);
        await _dbContext.SaveChangesAsync();

        _completedRun = CreateCompletedRun(
            _newerScopedCampaign,
            actorId: 102,
            requestedAtUtc: new DateTime(2026, 6, 15, 2, 0, 0, DateTimeKind.Utc),
            totalSelected: 3,
            succeeded: 2,
            failed: 1,
            skipped: 0,
            totalAmount: 100m);

        _outsideScopeRun = CreateCompletedRun(
            _outsideScopeCampaign,
            actorId: 103,
            requestedAtUtc: new DateTime(2026, 6, 16, 2, 0, 0, DateTimeKind.Utc),
            totalSelected: 1,
            succeeded: 1,
            failed: 0,
            skipped: 0,
            totalAmount: 50m);

        _dbContext.AddRange(_completedRun, _outsideScopeRun);
        await _dbContext.SaveChangesAsync();

        EducationAccount matchingAccount = EducationAccount.OpenManual(
            personId: 9001,
            accountNumber: "EA-HISTORY-MATCH",
            now: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            reason: "TEST",
            remarks: "History search test",
            openedBy: 102).Value;

        _dbContext.Add(matchingAccount);
        await _dbContext.SaveChangesAsync();

        TopUpTransaction transaction = TopUpTransaction.Create(
            _completedRun.Id,
            matchingAccount.Id,
            50m,
            new DateTime(2026, 6, 15, 2, 0, 0, DateTimeKind.Utc));
        transaction.Complete(7001, new DateTime(2026, 6, 15, 2, 1, 0, DateTimeKind.Utc));
        _dbContext.Add(transaction);
        await _dbContext.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        await _dbContext.DisposeAsync();
    }

    [Fact]
    public async Task Campaign_History_Should_Filter_Scope_And_Use_Stable_Pagination()
    {
        FakeCurrentUser user = new(
            permissions: [TopUpPermissions.Manage],
            organizationIds: [1]);
        GetCampaignHistoryHandler handler = new(
            new TopUpAccessScopeResolver(user),
            _reader);

        TopUpHistoryFilter filter = EmptyFilter();
        var result = await handler.Handle(
            new GetCampaignHistoryQuery(filter, Page: 1, PageSize: 1),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        PageResponse<CampaignHistoryItem> page = result.Value;
        page.TotalCount.Should().Be(2);
        page.Items.Should().ContainSingle();
        page.Items[0].CampaignId.Should().Be(_newerScopedCampaign.Id);
        page.Items.Should().NotContain(x => x.CampaignId == _outsideScopeCampaign.Id);
    }

    [Fact]
    public async Task Run_History_Should_Return_Reconciliation_Fields_And_Filter_By_Account()
    {
        FakeCurrentUser user = new(
            permissions: [TopUpPermissions.Manage],
            organizationIds: [1]);
        GetRunHistoryHandler handler = new(
            new TopUpAccessScopeResolver(user),
            _reader);

        TopUpHistoryFilter filter = EmptyFilter() with
        {
            StudentOrAccountSearch = "HISTORY-MATCH",
            Status = TopUpRunStatusCodes.Partial,
            TriggerType = TopUpRunTriggerTypes.Manual
        };

        var result = await handler.Handle(
            new GetRunHistoryQuery(filter, Page: 1, PageSize: 25),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        RunHistoryItem item = result.Value.Items.Should().ContainSingle().Subject;
        item.RunId.Should().Be(_completedRun.Id);
        item.MatchedCount.Should().Be(3);
        item.ProcessedCount.Should().Be(3);
        item.SucceededCount.Should().Be(2);
        item.FailedCount.Should().Be(1);
        item.TotalCredited.Should().Be(100m);
    }

    [Fact]
    public async Task History_Filter_Should_Apply_Identically_To_Total_And_Page_Items()
    {
        FakeCurrentUser user = new(
            permissions: [TopUpPermissions.ViewAll],
            organizationIds: []);
        GetCampaignHistoryHandler handler = new(
            new TopUpAccessScopeResolver(user),
            _reader);

        TopUpHistoryFilter filter = EmptyFilter() with
        {
            DateFromUtc = new DateTime(2026, 6, 9, 0, 0, 0, DateTimeKind.Utc),
            DateToUtc = new DateTime(2026, 6, 13, 0, 0, 0, DateTimeKind.Utc),
            CampaignSearch = "HISTORY",
            ActorId = 102
        };

        var result = await handler.Handle(
            new GetCampaignHistoryQuery(filter, Page: 1, PageSize: 25),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.TotalCount.Should().Be(result.Value.Items.Count);
        result.Value.Items.Should().ContainSingle()
            .Which.CampaignId.Should().Be(_newerScopedCampaign.Id);
    }

    [Fact]
    public async Task Cross_Scope_History_Request_Should_Be_Denied()
    {
        FakeCurrentUser user = new(
            permissions: [TopUpPermissions.Manage],
            organizationIds: [1]);
        GetRunHistoryHandler handler = new(
            new TopUpAccessScopeResolver(user),
            _reader);

        TopUpHistoryFilter filter = EmptyFilter() with { OrganizationId = 2 };
        var result = await handler.Handle(
            new GetRunHistoryQuery(filter, Page: 1, PageSize: 25),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(TopUpHistoryErrors.OrganizationOutsideScope);
    }

    private static TopUpCampaign CreateCampaign(
        long organizationId,
        string code,
        string name,
        long actorId,
        DateTime createdAtUtc)
    {
        TopUpCampaign campaign = TopUpCampaign.Create(
            organizationId,
            code,
            name,
            description: null,
            recipientModeCode: "FIXED_SELECTION",
            defaultTopUpAmount: 50m,
            reason: "History test",
            scheduleTypeCode: "IMMEDIATE",
            startDate: DateOnly.FromDateTime(createdAtUtc),
            endDate: null,
            frequencyCode: null,
            frequencyInterval: null,
            currentUserId: actorId,
            nowUtc: createdAtUtc);
        campaign.ChangeStatus(TopUpCampaignStatusCodes.Active, actorId, createdAtUtc);
        return campaign;
    }

    private static TopUpRun CreateCompletedRun(
        TopUpCampaign campaign,
        long actorId,
        DateTime requestedAtUtc,
        int totalSelected,
        int succeeded,
        int failed,
        int skipped,
        decimal totalAmount)
    {
        TopUpRun run = TopUpRun.CreateManual(
            campaign,
            $"history:{campaign.Id}:{requestedAtUtc.Ticks}",
            actorId,
            requestedAtUtc,
            note: null);
        run.SetTotalSelected(totalSelected).IsSuccess.Should().BeTrue();
        run.StartProcessing(requestedAtUtc).IsSuccess.Should().BeTrue();
        run.Finalize(
            succeeded + failed + skipped,
            succeeded,
            failed,
            skipped,
            totalAmount,
            requestedAtUtc.AddMinutes(1)).IsSuccess.Should().BeTrue();
        return run;
    }

    private static TopUpHistoryFilter EmptyFilter()
        => new(
            DateFromUtc: null,
            DateToUtc: null,
            CampaignId: null,
            CampaignSearch: null,
            OrganizationId: null,
            TriggerType: null,
            Status: null,
            StudentOrAccountSearch: null,
            ActorId: null);

    private sealed class FakeCurrentUser(
        IReadOnlyCollection<string> permissions,
        IReadOnlyCollection<long> organizationIds) : ICurrentUser
    {
        public long? UserAccountId => 100;
        public long? PersonId => null;
        public long? OrganizationUnitId => organizationIds.Count == 0
            ? null
            : organizationIds.First();
        public IReadOnlyCollection<long> OrganizationUnitIds => organizationIds;
        public IReadOnlyCollection<string> Roles => ["SYSTEM_ADMIN"];
        public IReadOnlyCollection<string> Permissions => permissions;
        public string Portal => "ADMIN";
        public bool IsAuthenticated => true;
        public bool HasPermission(string permission) => permissions.Contains(permission);
    }

    private sealed class HistoryTestModelConfiguration : IModelConfigurationContributor
    {
        public void Configure(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<TopUpCampaign>(builder =>
            {
                builder.HasKey(x => x.Id);
                builder.Property(x => x.Id).ValueGeneratedOnAdd();
            });

            modelBuilder.Entity<TopUpRun>(builder =>
            {
                builder.HasKey(x => x.Id);
                builder.Property(x => x.Id).ValueGeneratedOnAdd();
                builder.Ignore(x => x.ScheduledFor);
                builder.Ignore(x => x.StartedAt);
                builder.Ignore(x => x.CompletedAt);
                builder.Ignore(x => x.DomainEvents);
            });

            modelBuilder.Entity<TopUpTransaction>(builder =>
            {
                builder.HasKey(x => x.Id);
                builder.Property(x => x.Id).ValueGeneratedOnAdd();
            });

            modelBuilder.Entity<EducationAccount>(builder =>
            {
                builder.HasKey(x => x.Id);
                builder.Property(x => x.Id).ValueGeneratedOnAdd();
                builder.Ignore(x => x.DomainEvents);
            });

            modelBuilder.Entity<Person>(builder =>
            {
                builder.HasKey(x => x.Id);
                builder.Property(x => x.Id).ValueGeneratedOnAdd();
                builder.Ignore(x => x.DomainEvents);
            });
        }
    }
}
