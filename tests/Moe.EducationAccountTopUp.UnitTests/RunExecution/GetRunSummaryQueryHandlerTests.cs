using FluentAssertions;
using Moe.Application.Abstractions.Security;
using Moe.Modules.EducationAccountTopUp.Application.History;
using Moe.Modules.EducationAccountTopUp.Application.RunExecution;
using Moe.Modules.EducationAccountTopUp.Application.RunExecution.GetRunSummary;
using Moe.Modules.EducationAccountTopUp.Domain.TopUps;
using Moe.Modules.EducationAccountTopUp.IGateway.RunSummary;
using Xunit;

namespace Moe.EducationAccountTopUp.UnitTests.RunExecution;

public sealed class GetRunSummaryQueryHandlerTests
{
    private static readonly DateTime RunDateUtc =
        new(2026, 6, 18, 4, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Should_Return_Safe_Reconciled_Run_Summary()
    {
        RunSummaryProjection projection = CreateProjection(organizationId: 10);
        GetRunSummaryQueryHandler handler = CreateHandler(
            projection,
            permissions: ["TOPUPS_MANAGE"],
            organizationIds: [10]);

        var result = await handler.Handle(
            new GetRunSummaryQuery(projection.RunId),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.RunId.Should().Be(projection.RunId);
        result.Value.CampaignId.Should().Be(projection.CampaignId);
        result.Value.RunDateUtc.Should().Be(RunDateUtc);
        result.Value.TriggerType.Should().Be(TopUpRunTriggerTypes.Manual);
        result.Value.Status.Should().Be(TopUpRunStatusCodes.Partial);
        result.Value.MatchedCount.Should().Be(5);
        result.Value.ProcessedCount.Should().Be(5);
        result.Value.SucceededCount.Should().Be(3);
        result.Value.FailedCount.Should().Be(1);
        result.Value.SkippedCount.Should().Be(1);
        result.Value.TotalCredited.Should().Be(300m);
        result.Value.StartedAtUtc.Should().Be(RunDateUtc.AddMinutes(1));
        result.Value.CompletedAtUtc.Should().Be(RunDateUtc.AddMinutes(2));
    }

    [Fact]
    public async Task Should_Deny_Run_Outside_Organization_Scope()
    {
        RunSummaryProjection projection = CreateProjection(organizationId: 20);
        GetRunSummaryQueryHandler handler = CreateHandler(
            projection,
            permissions: ["TOPUPS_MANAGE"],
            organizationIds: [10]);

        var result = await handler.Handle(
            new GetRunSummaryQuery(projection.RunId),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(TopUpHistoryErrors.OrganizationOutsideScope);
    }

    [Fact]
    public async Task Should_Deny_Actor_Without_View_Capability()
    {
        RunSummaryProjection projection = CreateProjection(organizationId: 10);
        GetRunSummaryQueryHandler handler = CreateHandler(
            projection,
            permissions: [],
            organizationIds: [10],
            roles: []);

        var result = await handler.Handle(
            new GetRunSummaryQuery(projection.RunId),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(TopUpHistoryErrors.AccessDenied);
    }

    [Fact]
    public async Task Should_Allow_ViewAll_Actor_Across_Organizations()
    {
        RunSummaryProjection projection = CreateProjection(organizationId: 20);
        GetRunSummaryQueryHandler handler = CreateHandler(
            projection,
            permissions: ["TOPUP_VIEW_ALL"],
            organizationIds: [],
            roles: ["HQ_ADMIN"]);

        var result = await handler.Handle(
            new GetRunSummaryQuery(projection.RunId),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Should_Return_NotFound_When_Nested_Campaign_Does_Not_Match()
    {
        RunSummaryProjection projection = CreateProjection(organizationId: 10);
        GetRunSummaryQueryHandler handler = CreateHandler(
            projection,
            permissions: ["TOPUPS_MANAGE"],
            organizationIds: [10]);

        var result = await handler.Handle(
            new GetRunSummaryQuery(projection.RunId, ExpectedCampaignId: 999),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(TopUpErrors.RunNotFound);
    }

    [Fact]
    public async Task Should_Return_NotFound_When_Run_Does_Not_Exist()
    {
        GetRunSummaryQueryHandler handler = CreateHandler(
            projection: null,
            permissions: ["TOPUPS_MANAGE"],
            organizationIds: [10]);

        var result = await handler.Handle(
            new GetRunSummaryQuery(999),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(TopUpErrors.RunNotFound);
    }

    private static GetRunSummaryQueryHandler CreateHandler(
        RunSummaryProjection? projection,
        IReadOnlyCollection<string> permissions,
        IReadOnlyCollection<long> organizationIds,
        IReadOnlyCollection<string>? roles = null)
    {
        FakeCurrentUser currentUser = new(permissions, organizationIds, roles ?? ["SCHOOL_ADMIN"]);
        return new GetRunSummaryQueryHandler(
            new FakeRunSummaryReader(projection),
            new TopUpAccessScopeResolver(currentUser));
    }

    private static RunSummaryProjection CreateProjection(long organizationId)
        => new(
            RunId: 123,
            CampaignId: 456,
            OrganizationId: organizationId,
            RunDateUtc,
            TriggerType: TopUpRunTriggerTypes.Manual,
            Status: TopUpRunStatusCodes.Partial,
            MatchedCount: 5,
            ProcessedCount: 5,
            SucceededCount: 3,
            FailedCount: 1,
            SkippedCount: 1,
            TotalCredited: 300m,
            StartedAtUtc: RunDateUtc.AddMinutes(1),
            CompletedAtUtc: RunDateUtc.AddMinutes(2));

    private sealed class FakeRunSummaryReader(RunSummaryProjection? projection)
        : ITopUpRunSummaryReader
    {
        public Task<RunSummaryProjection?> GetByIdAsync(
            long runId,
            CancellationToken cancellationToken)
            => Task.FromResult(projection?.RunId == runId ? projection : null);
    }

    private sealed class FakeCurrentUser(
        IReadOnlyCollection<string> permissions,
        IReadOnlyCollection<long> organizationIds,
        IReadOnlyCollection<string> roles) : ICurrentUser
    {
        public long? UserAccountId => 1;
        public long? PersonId => null;
        public long? OrganizationUnitId => organizationIds.FirstOrDefault();
        public IReadOnlyCollection<long> OrganizationUnitIds => organizationIds;
        public IReadOnlyCollection<string> Roles => roles;
        public IReadOnlyCollection<string> Permissions => permissions;
        public string Portal => "ADMIN";
        public bool IsAuthenticated => true;

        public bool HasPermission(string permission)
            => permissions.Contains(permission, StringComparer.OrdinalIgnoreCase);
    }
}
