using FluentAssertions;
using Moe.Application.Abstractions.Audit;
using Moe.Application.Abstractions.Clock;
using Moe.Application.Abstractions.Persistence;
using Moe.Application.Abstractions.Security;
using Moe.Modules.IdentityPlatform.Application;
using Moe.Modules.IdentityPlatform.Application.Organizations;
using Moe.Modules.IdentityPlatform.Application.Students;
using Moe.Modules.IdentityPlatform.Application.Students.CreateStudent;
using Moe.Modules.IdentityPlatform.Domain.People;
using Moe.Modules.IdentityPlatform.Domain.Schooling;
using Moe.Modules.IdentityPlatform.IGateway.Repositories;
using Moe.SharedKernel.Results;
using Xunit;

namespace Moe.IdentityPlatform.UnitTests.Application.Students;

public sealed class CreateStudentHandlerEmailTests
{
    private readonly DateTimeOffset _now = new(2026, 7, 1, 4, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Handle_OnSuccessfulCreate_EnqueuesStudentAccountCreatedMail()
    {
        CreateStudentHandler handler = CreateHandler(out var scheduler);

        Result<CreateStudentResponse> result = await handler.Handle(ValidCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        scheduler.Jobs.Should().ContainSingle().Subject.NotificationType
            .Should().Be(StudentAccountNotificationEmailService.AccountCreatedNotificationType);
        scheduler.Jobs[0].PersonId.Should().Be(result.Value.PersonId);
        scheduler.Jobs[0].PlainTextBody.Should().Contain("Portal access ready");
    }

    [Fact]
    public async Task Handle_WhenStudentIdentityExists_DoesNotEnqueueMail()
    {
        FakeStudentOnboardingRepository students = new(identityExists: true);
        CreateStudentHandler handler = CreateHandler(out var scheduler, students);

        Result<CreateStudentResponse> result = await handler.Handle(ValidCommand(), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(IdentityErrors.StudentIdentityAlreadyExists);
        scheduler.Jobs.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WhenMailDeliveryDisabled_StillCreatesStudentAndSkipsMail()
    {
        CreateStudentHandler handler = CreateHandler(out var scheduler, mailEnabled: false);

        Result<CreateStudentResponse> result = await handler.Handle(ValidCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        scheduler.Jobs.Should().BeEmpty();
    }

    private CreateStudentHandler CreateHandler(
        out StudentAccountNotificationEmailServiceTests.RecordingEmailNotificationScheduler scheduler,
        FakeStudentOnboardingRepository? students = null,
        bool mailEnabled = true)
    {
        scheduler = new StudentAccountNotificationEmailServiceTests.RecordingEmailNotificationScheduler(mailEnabled);
        StudentAccountNotificationEmailService notifications = new(
            scheduler,
            new StudentAccountNotificationEmailServiceTests.TestEmailBrandingProvider());

        return new CreateStudentHandler(
            new FakeCurrentUser(),
            new FakeAdminAccessControl(),
            new TestClock(_now),
            new FakeOrganizationUnitRepository(),
            students ?? new FakeStudentOnboardingRepository(),
            new FakeUnitOfWork(),
            new ImmediateTransactionalExecutor(),
            new FakeAuditService(),
            notifications);
    }

    private static CreateStudentCommand ValidCommand()
        => new(
            SchoolName: null,
            OrganizationId: 1,
            IdentityNumber: "S1234567A",
            FullName: "Hannah Tan",
            DateOfBirth: new DateOnly(2000, 1, 1),
            NationalityCode: "SG",
            CitizenshipStatusCode: "CITIZEN",
            StudentNumber: "STU001",
            AcademicYear: "2026",
            LevelCode: "SEC_1",
            ClassCode: "1A",
            StartDate: new DateOnly(2026, 1, 1),
            Email: "hannah@example.com",
            ContactNumber: null,
            Address: null,
            IsAccountHolder: true);

    private sealed class FakeStudentOnboardingRepository(
        bool identityExists = false,
        bool studentNumberExists = false) : IStudentOnboardingRepository
    {
        private long _nextPersonId = 123;

        public Task<bool> IdentityNumberExistsAsync(byte[] identityNumberHash, CancellationToken cancellationToken)
            => Task.FromResult(identityExists);

        public Task<bool> StudentNumberExistsAsync(string studentNumber, CancellationToken cancellationToken)
            => Task.FromResult(studentNumberExists);

        public Task<long> AddPersonAsync(Person person, CancellationToken cancellationToken, bool saveChanges = true)
        {
            long personId = _nextPersonId++;
            typeof(Person).GetProperty(nameof(Person.Id))!.SetValue(person, personId);
            return Task.FromResult(personId);
        }

        public Task<CreatedStudentRecord> AddStudentIdentityAndEnrollmentAsync(
            PersonIdentifier identityNumber,
            SchoolEnrollment enrollment,
            CancellationToken cancellationToken,
            bool saveChanges = true)
            => Task.FromResult(new CreatedStudentRecord(enrollment.PersonId, 456));
    }

    private sealed class FakeOrganizationUnitRepository : IOrganizationUnitRepository
    {
        private static readonly OrganizationUnitSummary School = new(
            1,
            null,
            "NVS",
            "North View Secondary School",
            "SCHOOL",
            "ACTIVE");

        public Task<IReadOnlyCollection<OrganizationUnitSummary>> ListActiveAsync(
            IReadOnlyCollection<long>? organizationIds,
            CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyCollection<OrganizationUnitSummary>>([School]);

        public Task<OrganizationUnitSummary?> FindActiveByIdAsync(long organizationId, CancellationToken cancellationToken)
            => Task.FromResult<OrganizationUnitSummary?>(School);

        public Task<OrganizationUnitSummary?> FindActiveSchoolByNameAsync(string schoolName, CancellationToken cancellationToken)
            => Task.FromResult<OrganizationUnitSummary?>(School);

        public Task<OrganizationUnitSummary?> FindActiveSchoolByIdAsync(long organizationId, CancellationToken cancellationToken)
            => Task.FromResult<OrganizationUnitSummary?>(School);
    }

    private sealed class FakeCurrentUser : ICurrentUser
    {
        public long? UserAccountId => 99;
        public long? PersonId => null;
        public long? OrganizationUnitId => 1;
        public IReadOnlyCollection<long> OrganizationUnitIds => [1];
        public IReadOnlyCollection<string> Roles => [];
        public IReadOnlyCollection<string> Permissions => [];
        public string Portal => "ADMIN";
        public bool IsAuthenticated => true;
        public bool HasPermission(string permission) => true;
    }

    private sealed class FakeAdminAccessControl : IAdminAccessControl
    {
        public bool IsHqAdmin => false;
        public bool IsSchoolAdmin => true;
        public IReadOnlyCollection<long> ScopedOrganizationIds => [1];
        public bool CanAccessOrganization(long organizationId) => organizationId == 1;
        public Result EnsureCanAccessOrganization(long organizationId)
            => CanAccessOrganization(organizationId) ? Result.Success() : Result.Failure(IdentityErrors.SchoolOutsideScope);
        public AdminOrganizationScope ResolveOrganizationFilter(long? requestedOrganizationId)
            => new(true, false, requestedOrganizationId ?? 1, [1]);
    }

    private sealed class TestClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow { get; } = utcNow;

        public DateOnly TodayInSingapore() => SingaporeBusinessDay.FromUtc(UtcNow);
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(1);
    }

    private sealed class ImmediateTransactionalExecutor : ITransactionalExecutor
    {
        public Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default)
            => operation(cancellationToken);
    }

    private sealed class FakeAuditService : IAuditService
    {
        public Task RecordAsync(
            string actionCode,
            string entityTypeCode,
            string entityId,
            string? detailsJson = null,
            CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task RecordSchoolActionAsync(SchoolAuditContext context, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
