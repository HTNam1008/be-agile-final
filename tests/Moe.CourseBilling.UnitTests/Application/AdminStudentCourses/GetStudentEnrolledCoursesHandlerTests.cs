using FluentAssertions;
using Moe.Application.Abstractions.Security;
using Moe.Infrastructure.Shared.Api;
using Moe.Modules.CourseBilling.Application.AdminStudentCourses;
using Moe.Modules.CourseBilling.Domain.Courses;
using Moe.Modules.CourseBilling.IGateway.AdminStudentCourses;
using Moe.Modules.IdentityPlatform.IGateway.People;
using Moe.SharedKernel.Results;
using Xunit;

namespace Moe.CourseBilling.UnitTests.Application.AdminStudentCourses;

public sealed class GetStudentEnrolledCoursesHandlerTests
{
    private readonly FakePersonDirectory _people = new();
    private readonly FakeAdminAccessControl _adminAccess = new();
    private readonly FakeAdminStudentEnrolledCourseReader _reader = new();

    [Fact]
    public async Task Handle_OnHqAdminWithNoOrganization_ReturnsCoursesAndMapsStatusLabels()
    {
        _people.OrganizationByPersonId[5001] = null;
        _adminAccess.IsHqAdmin = true;
        _reader.Page = new PageResponse<AdminStudentEnrolledCourseProjection>(
            [
                new AdminStudentEnrolledCourseProjection(
                    10,
                    "Robotics",
                    CourseEnrollmentStatusCodes.PendingPayment,
                    new DateTime(2026, 6, 20, 8, 0, 0, DateTimeKind.Utc),
                    100m,
                    25m,
                    10m,
                    65m),
                new AdminStudentEnrolledCourseProjection(
                    11,
                    "Ceramics",
                    CourseEnrollmentStatusCodes.Exited,
                    new DateTime(2026, 6, 18, 8, 0, 0, DateTimeKind.Utc),
                    50m,
                    0m,
                    0m,
                    50m),
                new AdminStudentEnrolledCourseProjection(
                    12,
                    "Unknown",
                    "WAITLISTED",
                    new DateTime(2026, 6, 17, 8, 0, 0, DateTimeKind.Utc),
                    0m,
                    0m,
                    0m,
                    0m)
            ],
            1,
            20,
            3);
        GetStudentEnrolledCoursesHandler handler = CreateHandler();

        var result = await handler.Handle(
            new GetStudentEnrolledCoursesQuery(5001, Page: 1, PageSize: 20),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Select(x => x.StatusLabel).Should().Equal("Active", "Dropped Out", "WAITLISTED");
        result.Value.Items[0].Fee.Should().Be(100m);
        result.Value.Items[0].FasApplied.Should().Be(25m);
        result.Value.Items[0].Paid.Should().Be(10m);
        result.Value.Items[0].Outstanding.Should().Be(65m);
        _reader.Calls.Should().Be(1);
    }

    [Fact]
    public async Task Handle_OnSchoolAdminInScope_ReturnsCourses()
    {
        _people.OrganizationByPersonId[5002] = 10;
        _reader.Page = new PageResponse<AdminStudentEnrolledCourseProjection>(
            [
                new AdminStudentEnrolledCourseProjection(
                    20,
                    "Science",
                    CourseEnrollmentStatusCodes.Completed,
                    new DateTime(2026, 6, 19, 8, 0, 0, DateTimeKind.Utc),
                    80m,
                    0m,
                    80m,
                    0m)
            ],
            1,
            20,
            1);
        GetStudentEnrolledCoursesHandler handler = CreateHandler();

        var result = await handler.Handle(
            new GetStudentEnrolledCoursesQuery(5002, Page: 1, PageSize: 20),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().ContainSingle()
            .Which.StatusLabel.Should().Be("Completed");
        _reader.Calls.Should().Be(1);
    }

    [Fact]
    public async Task Handle_OnOutOfScopeSchoolAdmin_DeniesBeforeReadingCourses()
    {
        _people.OrganizationByPersonId[5003] = 20;
        _adminAccess.AccessibleOrganizationIds.Clear();
        _adminAccess.AccessibleOrganizationIds.Add(10);
        GetStudentEnrolledCoursesHandler handler = CreateHandler();

        var result = await handler.Handle(
            new GetStudentEnrolledCoursesQuery(5003, Page: 1, PageSize: 20),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("AUTH.ORGANIZATION_OUTSIDE_SCOPE");
        _reader.Calls.Should().Be(0);
    }

    [Fact]
    public async Task Handle_OnSchoolAdminWithNoOrganization_DeniesBeforeReadingCourses()
    {
        _people.OrganizationByPersonId[5004] = null;
        GetStudentEnrolledCoursesHandler handler = CreateHandler();

        var result = await handler.Handle(
            new GetStudentEnrolledCoursesQuery(5004, Page: 1, PageSize: 20),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("AUTH.ORGANIZATION_OUTSIDE_SCOPE");
        _reader.Calls.Should().Be(0);
    }

    [Fact]
    public async Task Handle_OnStudentNotFound_ReturnsPersonNotFound()
    {
        GetStudentEnrolledCoursesHandler handler = CreateHandler();

        var result = await handler.Handle(
            new GetStudentEnrolledCoursesQuery(9999, Page: 1, PageSize: 20),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(CourseBillingErrors.PersonNotFound);
        _reader.Calls.Should().Be(0);
    }

    private GetStudentEnrolledCoursesHandler CreateHandler()
        => new(_people, _adminAccess, _reader);

    private sealed class FakePersonDirectory : IPersonDirectory
    {
        public Dictionary<long, long?> OrganizationByPersonId { get; } = [];

        public Task<PersonSummary?> FindAsync(long personId, CancellationToken cancellationToken)
        {
            if (!OrganizationByPersonId.TryGetValue(personId, out long? organizationId))
            {
                return Task.FromResult<PersonSummary?>(null);
            }

            return Task.FromResult<PersonSummary?>(new PersonSummary(
                personId,
                "Test Student",
                new DateOnly(2010, 1, 1),
                "SG",
                "CITIZEN",
                organizationId));
        }
    }

    private sealed class FakeAdminAccessControl : IAdminAccessControl
    {
        private static readonly Error OrganizationOutsideScope = new(
            "AUTH.ORGANIZATION_OUTSIDE_SCOPE",
            "The requested organization is outside the current admin's scope.");

        public bool IsHqAdmin { get; set; }
        public bool IsSchoolAdmin => !IsHqAdmin;
        public HashSet<long> AccessibleOrganizationIds { get; } = [10];
        public IReadOnlyCollection<long> ScopedOrganizationIds => AccessibleOrganizationIds;

        public bool CanAccessOrganization(long organizationId)
            => IsHqAdmin || AccessibleOrganizationIds.Contains(organizationId);

        public Result EnsureCanAccessOrganization(long organizationId)
            => CanAccessOrganization(organizationId)
                ? Result.Success()
                : Result.Failure(OrganizationOutsideScope);

        public AdminOrganizationScope ResolveOrganizationFilter(long? requestedOrganizationId)
            => new(true, IsHqAdmin, requestedOrganizationId, AccessibleOrganizationIds);
    }

    private sealed class FakeAdminStudentEnrolledCourseReader : IAdminStudentEnrolledCourseReader
    {
        public int Calls { get; private set; }
        public PageResponse<AdminStudentEnrolledCourseProjection> Page { get; set; } = new([], 1, 20, 0);

        public Task<PageResponse<AdminStudentEnrolledCourseProjection>> ListAsync(
            long personId,
            int page,
            int pageSize,
            CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(Page);
        }
    }
}
