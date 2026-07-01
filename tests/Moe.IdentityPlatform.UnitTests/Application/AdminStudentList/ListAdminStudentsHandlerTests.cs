using FluentAssertions;
using Moe.Application.Abstractions.Clock;
using Moe.Application.Abstractions.Security;
using Moe.Modules.IdentityPlatform.Application;
using Moe.Modules.IdentityPlatform.Application.AdminStudentList;
using Moe.Modules.IdentityPlatform.IGateway.Students;
using Moe.SharedKernel.Results;
using Xunit;

namespace Moe.IdentityPlatform.UnitTests.Application.AdminStudentList;

public sealed class ListAdminStudentsHandlerTests
{
    [Fact]
    public async Task Handle_WhenSchoolAdminRequestsOutOfScopeOrganization_ReturnsExplicitScopeError()
    {
        FakeAdminAccessControl adminAccess = new(isHqAdmin: false, scopedOrganizationIds: [10]);
        FakeAdminStudentListReader reader = new();
        ListAdminStudentsHandler handler = new(reader, adminAccess, new TestClock());

        Result<AdminStudentListPage> result = await handler.Handle(
            new ListAdminStudentsQuery(
                OrganizationId: 20,
                Search: null,
                LevelCodes: [],
                ClassCode: null,
                AccountStatus: AdminStudentAccountStatusFilter.All,
                PortalAccessStatus: AdminStudentPortalAccessStatusFilter.All,
                EnrollmentStatus: AdminStudentEnrollmentStatusFilter.All,
                Page: 1,
                PageSize: 20,
                SortBy: null,
                SortDirection: null),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(IdentityErrors.OrganizationOutsideScope);
        reader.ListCalls.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WhenSchoolAdminOmitsOrganization_AppliesScopedOrganizationsWithoutError()
    {
        FakeAdminAccessControl adminAccess = new(isHqAdmin: false, scopedOrganizationIds: [10, 20]);
        FakeAdminStudentListReader reader = new();
        ListAdminStudentsHandler handler = new(reader, adminAccess, new TestClock());

        Result<AdminStudentListPage> result = await handler.Handle(
            new ListAdminStudentsQuery(
                OrganizationId: null,
                Search: null,
                LevelCodes: [],
                ClassCode: null,
                AccountStatus: AdminStudentAccountStatusFilter.All,
                PortalAccessStatus: AdminStudentPortalAccessStatusFilter.All,
                EnrollmentStatus: AdminStudentEnrollmentStatusFilter.All,
                Page: 1,
                PageSize: 20,
                SortBy: null,
                SortDirection: null),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        reader.ListCalls.Should().Be(1);
        reader.LastScopedOrganizationIds.Should().BeEquivalentTo([10L, 20L]);
        reader.LastHasGlobalAccess.Should().BeFalse();
        reader.LastCriteria!.OrganizationId.Should().BeNull();
    }

    private sealed class FakeAdminStudentListReader : IAdminStudentListReader
    {
        public int ListCalls { get; private set; }
        public IReadOnlyCollection<long> LastScopedOrganizationIds { get; private set; } = [];
        public bool LastHasGlobalAccess { get; private set; }
        public AdminStudentListCriteria? LastCriteria { get; private set; }

        public Task<AdminStudentListPage> ListAsync(
            AdminStudentListCriteria criteria,
            IReadOnlyCollection<long> scopedOrganizationIds,
            bool hasGlobalAccess,
            DateOnly today,
            CancellationToken cancellationToken)
        {
            ListCalls++;
            LastCriteria = criteria;
            LastScopedOrganizationIds = scopedOrganizationIds;
            LastHasGlobalAccess = hasGlobalAccess;
            return Task.FromResult(new AdminStudentListPage([], criteria.Page, criteria.PageSize, 0));
        }

        public Task<IReadOnlyList<string>> ListClassesAsync(
            long organizationId,
            string levelCode,
            DateOnly today,
            CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<string>>([]);
    }

    private sealed class FakeAdminAccessControl(
        bool isHqAdmin,
        IReadOnlyCollection<long> scopedOrganizationIds) : IAdminAccessControl
    {
        public bool IsHqAdmin { get; } = isHqAdmin;
        public bool IsSchoolAdmin => !IsHqAdmin;
        public IReadOnlyCollection<long> ScopedOrganizationIds { get; } = scopedOrganizationIds;

        public bool CanAccessOrganization(long organizationId)
            => organizationId > 0 && (IsHqAdmin || ScopedOrganizationIds.Contains(organizationId));

        public Result EnsureCanAccessOrganization(long organizationId)
            => CanAccessOrganization(organizationId)
                ? Result.Success()
                : Result.Failure(IdentityErrors.OrganizationOutsideScope);

        public AdminOrganizationScope ResolveOrganizationFilter(long? requestedOrganizationId)
        {
            if (IsHqAdmin)
            {
                return requestedOrganizationId is long requested
                    ? new AdminOrganizationScope(requested > 0, true, requested, [])
                    : new AdminOrganizationScope(true, true, null, []);
            }

            if (requestedOrganizationId is long requestedOrganization)
            {
                return new AdminOrganizationScope(
                    ScopedOrganizationIds.Contains(requestedOrganization),
                    false,
                    requestedOrganization,
                    ScopedOrganizationIds);
            }

            return new AdminOrganizationScope(true, false, null, ScopedOrganizationIds);
        }
    }

    private sealed class TestClock : IClock
    {
        public DateTimeOffset UtcNow => new(2026, 6, 22, 8, 0, 0, TimeSpan.Zero);

        public DateOnly TodayInSingapore() => SingaporeBusinessDay.FromUtc(UtcNow);
    }
}
