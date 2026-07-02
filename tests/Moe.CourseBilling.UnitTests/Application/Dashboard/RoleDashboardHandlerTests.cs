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
            12, 10, 340, 320, [new AdminDashboardCountPoint(2, 20)], []));
        FakeFinanceReader finance = new(new AdminDashboardHqFinanceMetrics(
            300, 320, [new AdminDashboardFinanceCountPoint(2, 15)]));
        GetHqDashboardHandler handler = new(
            access,
            new FakeClock(),
            identities,
            finance);

        Result<HqDashboardResponse> result = await handler.Handle(
            new GetHqDashboardQuery(2026),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Cards.Should().Be(new HqDashboardCardsResponse(
            new DashboardCountMetricResponse(12, 20m),
            new DashboardCountMetricResponse(340, 6.3m),
            new DashboardCountMetricResponse(300, -6.3m)));
        result.Value.YearlyGrowth.Points.Should().HaveCount(12);
        result.Value.YearlyGrowth.Points.ElementAt(0).Should().Be(new HqDashboardMonthlyGrowthPoint(1, 0, 0));
        result.Value.YearlyGrowth.Points.ElementAt(1).Should().Be(new HqDashboardMonthlyGrowthPoint(2, 20, 15));
    }

    [Fact]
    public async Task SchoolDashboard_UsesSignedInSchoolScopeAndReturnsTopUpSeries()
    {
        FakeAdminAccessControl access = new() { IsSchoolAdmin = true, OrganizationId = 25 };
        FakeIdentityReader identities = new(new AdminDashboardIdentityMetrics(
            0, 0, 82, 80, [], [new AdminDashboardNullableCountPoint(3, 81)]));
        FakeFinanceReader finance = new(new AdminDashboardSchoolFinanceMetrics(
            900m, 1000m, "SGD", [new AdminDashboardFinanceAmountPoint(3, 250m)]));
        GetSchoolDashboardHandler handler = new(
            access,
            new FakeClock(),
            identities,
            finance,
            new FakeCourseReader(5));

        Result<SchoolDashboardResponse> result = await handler.Handle(
            new GetSchoolDashboardQuery(2026),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        identities.LastOrganizationId.Should().Be(25);
        finance.LastOrganizationId.Should().Be(25);
        result.Value.Cards.Should().Be(new SchoolDashboardCardsResponse(
            new DashboardCountMetricResponse(82, 2.5m),
            new DashboardCountMetricResponse(5, null),
            new DashboardAmountMetricResponse(900m, "SGD", -10m)));
        result.Value.YearlyMetrics.Points.Should().HaveCount(12);
        result.Value.YearlyMetrics.Points.ElementAt(2).Should().Be(new SchoolDashboardMonthlyMetricsPoint(3, 81, 250m));
        result.Value.YearlyMetrics.Points.ElementAt(7).Should().Be(new SchoolDashboardMonthlyMetricsPoint(8, null, null));
    }

    [Fact]
    public async Task HqDashboard_OnSchoolAdmin_ReturnsForbiddenRoleError()
    {
        GetHqDashboardHandler handler = new(
            new FakeAdminAccessControl { IsSchoolAdmin = true },
            new FakeClock(),
            new FakeIdentityReader(EmptyIdentity()),
            new FakeFinanceReader(EmptyHqFinance()));

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
            new FakeFinanceReader(EmptySchoolFinance()),
            new FakeCourseReader(0));

        Result<SchoolDashboardResponse> result = await handler.Handle(
            new GetSchoolDashboardQuery(2026),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("DASHBOARD.SCHOOL_SCOPE_REQUIRED");
    }

    [Theory]
    [InlineData(105, 100, 5)]
    [InlineData(88, 100, -12)]
    [InlineData(100, 100, 0)]
    public void DashboardTrend_CalculatesSignedPercentage(long current, long previous, double expected)
    {
        DashboardTrend.Calculate(current, previous).Should().Be((decimal)expected);
    }

    [Fact]
    public void DashboardTrend_WithoutPreviousBaseline_ReturnsNull()
    {
        DashboardTrend.Calculate(10, 0).Should().BeNull();
    }

    private static AdminDashboardIdentityMetrics EmptyIdentity() => new(0, 0, 0, 0, [], []);

    private static AdminDashboardHqFinanceMetrics EmptyHqFinance() => new(0, 0, []);

    private static AdminDashboardSchoolFinanceMetrics EmptySchoolFinance() => new(0, 0, "SGD", []);

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

    private sealed class FakeFinanceReader : IAdminDashboardFinanceMetricsReader
    {
        private readonly AdminDashboardHqFinanceMetrics hqMetrics;
        private readonly AdminDashboardSchoolFinanceMetrics schoolMetrics;

        public FakeFinanceReader(AdminDashboardHqFinanceMetrics metrics)
        {
            hqMetrics = metrics;
            schoolMetrics = EmptySchoolFinance();
        }

        public FakeFinanceReader(AdminDashboardSchoolFinanceMetrics metrics)
        {
            hqMetrics = EmptyHqFinance();
            schoolMetrics = metrics;
        }

        public long? LastOrganizationId { get; private set; }

        public Task<AdminDashboardHqFinanceMetrics> GetHqMetricsAsync(int year, DateTimeOffset now, CancellationToken cancellationToken)
            => Task.FromResult(hqMetrics);

        public Task<AdminDashboardSchoolFinanceMetrics> GetSchoolMetricsAsync(long organizationId, int year, DateTimeOffset now, CancellationToken cancellationToken)
        {
            LastOrganizationId = organizationId;
            return Task.FromResult(schoolMetrics);
        }
    }

    private sealed class FakeCourseReader(long totalCourses)
        : IAdminDashboardCourseMetricsReader
    {
        public Task<long> CountTotalCoursesAsync(long organizationId, CancellationToken cancellationToken)
            => Task.FromResult(totalCourses);

    }
}
