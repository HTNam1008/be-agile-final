using FluentAssertions;
using Moe.Application.Abstractions.Clock;
using Moe.Application.Abstractions.Security;
using Moe.Modules.CourseBilling.Application.Dashboard.RoleDashboards;
using Moe.Modules.CourseBilling.IGateway.Dashboard;
using Moe.Modules.EducationAccountTopUp.IGateway.Dashboard;
using Moe.Modules.IdentityPlatform.IGateway.Dashboard;
using Moe.SharedKernel.Results;
using Xunit;

namespace Moe.CourseBilling.UnitTests.Application.Dashboard;

public sealed class RoleDashboardHandlerTests
{
    [Fact]
    public async Task HqDashboard_ReturnsRealMetricsAndZeroFillsMissingMonths()
    {
        FakeAdminAccessControl access = new() { IsHqAdmin = true };
        FakeIdentityReader identities = new(new AdminDashboardIdentityMetrics(
            12, 340, 18, [new AdminDashboardCountPoint(2, 20)]));
        FakeFinanceReader finance = new(new AdminDashboardFinanceMetrics(
            300, 11, 4567m, 0, "SGD",
            [new AdminDashboardFinanceCountPoint(2, 15)],
            []));
        GetHqDashboardHandler handler = new(
            access,
            new FakeClock(),
            identities,
            finance,
            new FakeCourseReader(8, 0),
            new FakeFasReader(6));

        Result<HqDashboardResponse> result = await handler.Handle(
            new GetHqDashboardQuery(2026),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Cards.Should().Be(new HqDashboardCardsResponse(12, 340, 300, 8));
        result.Value.YearlyGrowth.Points.Should().HaveCount(12);
        result.Value.YearlyGrowth.Points.ElementAt(0).Should().Be(new HqDashboardMonthlyGrowthPoint(1, 0, 0));
        result.Value.YearlyGrowth.Points.ElementAt(1).Should().Be(new HqDashboardMonthlyGrowthPoint(2, 20, 15));
        result.Value.Overview.Should().Be(new HqDashboardOverviewResponse(18, 11, 6, 4567m, "SGD"));
    }

    [Fact]
    public async Task SchoolDashboard_UsesSignedInSchoolScopeAndReturnsTopUpSeries()
    {
        FakeAdminAccessControl access = new() { IsSchoolAdmin = true, OrganizationId = 25 };
        FakeIdentityReader identities = new(new AdminDashboardIdentityMetrics(0, 82, 4, []));
        FakeFinanceReader finance = new(new AdminDashboardFinanceMetrics(
            70, 0, 900m, 3, "SGD", [], [new AdminDashboardFinanceAmountPoint(3, 250m)]));
        GetSchoolDashboardHandler handler = new(
            access,
            new FakeClock(),
            identities,
            finance,
            new FakeCourseReader(5, 61),
            new FakeFasReader(7));

        Result<SchoolDashboardResponse> result = await handler.Handle(
            new GetSchoolDashboardQuery(2026),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        identities.LastOrganizationId.Should().Be(25);
        finance.LastOrganizationId.Should().Be(25);
        result.Value.Cards.Should().Be(new SchoolDashboardCardsResponse(82, 5, 70, 900m, "SGD"));
        result.Value.TopUpYearly.Points.Should().HaveCount(12);
        result.Value.TopUpYearly.Points.ElementAt(2).Amount.Should().Be(250m);
        result.Value.Overview.Should().Be(new SchoolDashboardOverviewResponse(4, 61, 7, 3));
    }

    [Fact]
    public async Task HqDashboard_OnSchoolAdmin_ReturnsForbiddenRoleError()
    {
        GetHqDashboardHandler handler = new(
            new FakeAdminAccessControl { IsSchoolAdmin = true },
            new FakeClock(),
            new FakeIdentityReader(EmptyIdentity()),
            new FakeFinanceReader(EmptyFinance()),
            new FakeCourseReader(0, 0),
            new FakeFasReader(0));

        Result<HqDashboardResponse> result = await handler.Handle(
            new GetHqDashboardQuery(2026),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("DASHBOARD.HQ_ADMIN_REQUIRED");
    }

    [Fact]
    public async Task SchoolDashboard_WithoutSingleSchoolScope_ReturnsScopeError()
    {
        GetSchoolDashboardHandler handler = new(
            new FakeAdminAccessControl { IsSchoolAdmin = true },
            new FakeClock(),
            new FakeIdentityReader(EmptyIdentity()),
            new FakeFinanceReader(EmptyFinance()),
            new FakeCourseReader(0, 0),
            new FakeFasReader(0));

        Result<SchoolDashboardResponse> result = await handler.Handle(
            new GetSchoolDashboardQuery(2026),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("DASHBOARD.SCHOOL_SCOPE_REQUIRED");
    }

    private static AdminDashboardIdentityMetrics EmptyIdentity() => new(0, 0, 0, []);

    private static AdminDashboardFinanceMetrics EmptyFinance() => new(0, 0, 0, 0, "SGD", [], []);

    private sealed class FakeClock : IClock
    {
        public DateTimeOffset UtcNow => new(2026, 7, 1, 2, 0, 0, TimeSpan.Zero);
        public DateOnly TodayInSingapore() => new(2026, 7, 1);
    }

    private sealed class FakeAdminAccessControl : IAdminAccessControl
    {
        public bool IsHqAdmin { get; set; }
        public bool IsSchoolAdmin { get; set; }
        public long? OrganizationId { get; set; }
        public IReadOnlyCollection<long> ScopedOrganizationIds => OrganizationId is long id ? [id] : [];
        public bool CanAccessOrganization(long organizationId) => IsHqAdmin || OrganizationId == organizationId;
        public Result EnsureCanAccessOrganization(long organizationId) => Result.Success();
        public AdminOrganizationScope ResolveOrganizationFilter(long? requestedOrganizationId)
            => OrganizationId is long id
                ? new AdminOrganizationScope(true, false, id, [id])
                : new AdminOrganizationScope(false, false, null, []);
    }

    private sealed class FakeIdentityReader(AdminDashboardIdentityMetrics metrics)
        : IAdminDashboardIdentityMetricsReader
    {
        public long? LastOrganizationId { get; private set; }

        public Task<AdminDashboardIdentityMetrics> GetHqMetricsAsync(int year, DateTimeOffset now, CancellationToken cancellationToken)
            => Task.FromResult(metrics);

        public Task<AdminDashboardIdentityMetrics> GetSchoolMetricsAsync(long organizationId, int year, DateTimeOffset now, CancellationToken cancellationToken)
        {
            LastOrganizationId = organizationId;
            return Task.FromResult(metrics);
        }
    }

    private sealed class FakeFinanceReader(AdminDashboardFinanceMetrics metrics)
        : IAdminDashboardFinanceMetricsReader
    {
        public long? LastOrganizationId { get; private set; }

        public Task<AdminDashboardFinanceMetrics> GetHqMetricsAsync(int year, DateTimeOffset now, CancellationToken cancellationToken)
            => Task.FromResult(metrics);

        public Task<AdminDashboardFinanceMetrics> GetSchoolMetricsAsync(long organizationId, int year, DateTimeOffset now, CancellationToken cancellationToken)
        {
            LastOrganizationId = organizationId;
            return Task.FromResult(metrics);
        }
    }

    private sealed class FakeCourseReader(long activeCourses, long activeEnrollments)
        : IAdminDashboardCourseMetricsReader
    {
        public Task<long> CountActiveCoursesAsync(long? organizationId, DateOnly currentDate, CancellationToken cancellationToken)
            => Task.FromResult(activeCourses);

        public Task<long> CountActiveEnrollmentsAsync(long organizationId, DateOnly currentDate, CancellationToken cancellationToken)
            => Task.FromResult(activeEnrollments);
    }

    private sealed class FakeFasReader(long pendingApplications) : IAdminDashboardFasMetricsReader
    {
        public Task<long> CountPendingApplicationsAsync(long? organizationId, CancellationToken cancellationToken)
            => Task.FromResult(pendingApplications);
    }
}
