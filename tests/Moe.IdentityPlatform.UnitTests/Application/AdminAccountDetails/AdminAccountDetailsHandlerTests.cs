using System.Text.Json;
using FluentAssertions;
using FluentValidation;
using Moe.Application.Abstractions.Audit;
using Moe.Application.Abstractions.Clock;
using Moe.Application.Abstractions.Persistence;
using Moe.Application.Abstractions.Security;
using Moe.Modules.IdentityPlatform.Application.AdminAccountDetails;
using Moe.Modules.IdentityPlatform.IGateway.Accounts;
using Moe.Modules.IdentityPlatform.IGateway.Repositories;
using Moe.SharedKernel.Results;
using Xunit;

namespace Moe.IdentityPlatform.UnitTests.Application.AdminAccountDetails;

public sealed class AdminAccountDetailsHandlerTests
{
    private readonly FakeAdminAccountDetailsRepository _profiles = new();
    private readonly FakeEducationAccountLookupGateway _accounts = new();
    private readonly FakeAdminAccessControl _adminAccess = new();
    private readonly FakeCurrentUser _currentUser = new();
    private readonly TestClock _clock = new(new DateTimeOffset(2026, 6, 22, 8, 0, 0, TimeSpan.Zero));
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeAuditService _audit = new();

    [Fact]
    public async Task Get_OnOwnSchoolStudent_ReturnsAccountDetails()
    {
        _profiles.DetailsByPersonId[5001] = CreateProfile(personId: 5001, organizationId: 10);
        _accounts.AccountsByPersonId[5001] = new EducationAccountLookupSummary(1001, 5001, "EA-1001", "SGD", "ACTIVE", 125.75m);
        GetAdminAccountDetailsHandler handler = CreateGetHandler();

        Result<AdminAccountDetailsResponse> result = await handler.Handle(new GetAdminAccountDetailsQuery(5001), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.PersonId.Should().Be(5001);
        result.Value.EducationAccountId.Should().Be(1001);
        result.Value.IdentityNumberMasked.Should().Be("S****123A");
        result.Value.MailingAddress.Should().Be("Official address");
        result.Value.ResidentialAddress.Should().Be("Preferred address");
        result.Value.ClassCode.Should().Be("1A");
        result.Value.AccountStatusCode.Should().Be("ACTIVE");
        result.Value.CurrentBalance.Should().Be(125.75m);
    }

    [Fact]
    public async Task Get_OnAccountHolderWithNoOrganization_SchoolAdminDenied()
    {
        _profiles.DetailsByPersonId[5002] = CreateProfile(personId: 5002, organizationId: null);
        _accounts.AccountsByPersonId[5002] = new EducationAccountLookupSummary(1002, 5002, "EA-1002", "SGD", "ACTIVE", 0m);
        GetAdminAccountDetailsHandler handler = CreateGetHandler();

        Result<AdminAccountDetailsResponse> result = await handler.Handle(new GetAdminAccountDetailsQuery(5002), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("AUTH.ORGANIZATION_OUTSIDE_SCOPE");
    }

    [Fact]
    public async Task Get_OnOutOfScopeStudent_ReturnsAccessDenied()
    {
        _profiles.DetailsByPersonId[5008] = CreateProfile(personId: 5008, organizationId: 20);
        _accounts.AccountsByPersonId[5008] = new EducationAccountLookupSummary(1008, 5008, "EA-1008", "SGD", "ACTIVE", 0m);
        GetAdminAccountDetailsHandler handler = CreateGetHandler();

        Result<AdminAccountDetailsResponse> result = await handler.Handle(new GetAdminAccountDetailsQuery(5008), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("AUTH.ORGANIZATION_OUTSIDE_SCOPE");
    }

    [Fact]
    public async Task Get_OnMissingPerson_ReturnsNotFound()
    {
        GetAdminAccountDetailsHandler handler = CreateGetHandler();

        Result<AdminAccountDetailsResponse> result = await handler.Handle(new GetAdminAccountDetailsQuery(5999), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("IDENTITY.PERSON_NOT_FOUND");
    }

    [Fact]
    public async Task Get_OnStudentWithoutEducationAccount_ReturnsProfileWithNoAccountStatus()
    {
        _profiles.DetailsByPersonId[5011] = CreateProfile(personId: 5011, organizationId: 10);
        GetAdminAccountDetailsHandler handler = CreateGetHandler();

        Result<AdminAccountDetailsResponse> result = await handler.Handle(new GetAdminAccountDetailsQuery(5011), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.PersonId.Should().Be(5011);
        result.Value.EducationAccountId.Should().BeNull();
        result.Value.AccountNumber.Should().BeNull();
        result.Value.DateOfBirth.Should().Be(new DateOnly(2010, 1, 1));
        result.Value.NationalityCode.Should().Be("SG");
        result.Value.SchoolOrganizationName.Should().Be("School One");
        result.Value.AcademicYear.Should().Be("2026");
        result.Value.AccountStatusCode.Should().Be("NO_ACCOUNT");
        result.Value.CurrentBalance.Should().BeNull();
    }

    [Fact]
    public async Task Get_OnAccountHolderWithNoOrganization_HqAdminAllowed()
    {
        _adminAccess.IsHqAdmin = true;
        _profiles.DetailsByPersonId[5003] = CreateProfile(personId: 5003, organizationId: null, classCode: null);
        _accounts.AccountsByPersonId[5003] = new EducationAccountLookupSummary(1003, 5003, "EA-1003", "SGD", "ACTIVE", 0m);
        GetAdminAccountDetailsHandler handler = CreateGetHandler();

        Result<AdminAccountDetailsResponse> result = await handler.Handle(new GetAdminAccountDetailsQuery(5003), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.SchoolOrganizationId.Should().BeNull();
        result.Value.ClassCode.Should().BeNull();
    }

    [Fact]
    public async Task Update_OnSuccess_AuditsChangedFieldNamesAndSavesOnce()
    {
        _profiles.UpdateResult = AdminAccountDetailsUpdateResult.Updated(
            CreateProfile(personId: 5004, organizationId: 10, classCode: "1B", preferredEmail: "new@example.sg"),
            ["classCode", "email"]);
        _profiles.DetailsByPersonId[5004] = CreateProfile(personId: 5004, organizationId: 10);
        _accounts.AccountsByPersonId[5004] = new EducationAccountLookupSummary(1004, 5004, "EA-1004", "SGD", "ACTIVE", 40m);
        UpdateAdminAccountDetailsHandler handler = CreateUpdateHandler();

        Result<AdminAccountDetailsResponse> result = await handler.Handle(
            new UpdateAdminAccountDetailsCommand(
                5004,
                "1B",
                "Preferred address",
                "new@example.sg",
                "+65 8123 4567",
                new DateTime(2026, 6, 22, 7, 0, 0, DateTimeKind.Utc)),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _audit.SchoolCalls.Should().ContainSingle();
        SchoolAuditContext call = _audit.SchoolCalls.Single();
        call.ActionCode.Should().Be("ACCOUNT_DETAILS_UPDATED_BY_ADMIN");
        call.EntityTypeCode.Should().Be("Person");
        call.EntityId.Should().Be(5004);
        call.SchoolOrganizationId.Should().Be(10);
        using JsonDocument document = JsonDocument.Parse(call.Details!.ToJson(call.EntityId));
        JsonElement root = document.RootElement;
        root.EnumerateObject().Select(x => x.Name).Should().BeEquivalentTo("summary", "entityId", "entityDisplayName", "changedFields");
        root.GetProperty("entityId").GetInt64().Should().Be(5004);
        root.GetProperty("changedFields").EnumerateArray().Select(x => x.GetString()).Should().BeEquivalentTo("classCode", "email");
        call.Details.ToJson(call.EntityId).Should().NotContain("new@example.sg");
        _unitOfWork.SaveCalls.Should().Be(1);
    }

    [Fact]
    public async Task Update_WhenClassCodeProvidedWithoutActiveEnrollment_ReturnsBusinessError()
    {
        _profiles.UpdateResult = AdminAccountDetailsUpdateResult.ClassEnrollmentMissing();
        _profiles.DetailsByPersonId[5005] = CreateProfile(personId: 5005, organizationId: null, classCode: null);
        _adminAccess.IsHqAdmin = true;
        UpdateAdminAccountDetailsHandler handler = CreateUpdateHandler();

        Result<AdminAccountDetailsResponse> result = await handler.Handle(
            new UpdateAdminAccountDetailsCommand(
                5005,
                "1C",
                "Preferred address",
                "parent@example.sg",
                "+65 8123 4567",
                new DateTime(2026, 6, 22, 7, 0, 0, DateTimeKind.Utc)),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("IDENTITY.ACTIVE_SCHOOL_ENROLLMENT_REQUIRED");
        _audit.Calls.Should().BeEmpty();
        _unitOfWork.SaveCalls.Should().Be(0);
    }

    [Fact]
    public async Task Update_OnOutOfScopeStudent_DoesNotAuditOrSave()
    {
        _profiles.DetailsByPersonId[5009] = CreateProfile(personId: 5009, organizationId: 20);
        _profiles.UpdateResult = AdminAccountDetailsUpdateResult.Updated(
            CreateProfile(personId: 5009, organizationId: 20, classCode: "1F"),
            ["classCode"]);
        UpdateAdminAccountDetailsHandler handler = CreateUpdateHandler();

        Result<AdminAccountDetailsResponse> result = await handler.Handle(
            new UpdateAdminAccountDetailsCommand(
                5009,
                "1F",
                "Preferred address",
                "student@example.sg",
                "+65 8123 4567",
                new DateTime(2026, 6, 22, 7, 0, 0, DateTimeKind.Utc)),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("AUTH.ORGANIZATION_OUTSIDE_SCOPE");
        _audit.Calls.Should().BeEmpty();
        _unitOfWork.SaveCalls.Should().Be(0);
    }

    [Theory]
    [InlineData("not-an-email", "+65 8123 4567", "Preferred address")]
    [InlineData("student@example.sg", "abc123", "Preferred address")]
    [InlineData("student@example.sg", "+65 8123 4567", LongAddress)]
    public void UpdateValidator_OnInvalidEditableFields_ReturnsValidationFailure(
        string email,
        string contactNumber,
        string residentialAddress)
    {
        var validator = new UpdateAdminAccountDetailsValidator();

        var result = validator.Validate(new UpdateAdminAccountDetailsCommand(
            5010,
            "1A",
            residentialAddress,
            email,
            contactNumber,
            new DateTime(2026, 6, 22, 7, 0, 0, DateTimeKind.Utc)));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Update_WhenConcurrencyTokenIsStale_ReturnsConflictWithoutAudit()
    {
        _profiles.UpdateResult = AdminAccountDetailsUpdateResult.Conflict();
        _profiles.DetailsByPersonId[5006] = CreateProfile(personId: 5006, organizationId: 10);
        UpdateAdminAccountDetailsHandler handler = CreateUpdateHandler();

        Result<AdminAccountDetailsResponse> result = await handler.Handle(
            new UpdateAdminAccountDetailsCommand(
                5006,
                "1D",
                "Preferred address",
                "student@example.sg",
                "+65 8123 4567",
                new DateTime(2026, 6, 22, 7, 0, 0, DateTimeKind.Utc)),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("IDENTITY.PROFILE_UPDATE_CONFLICT");
        _audit.Calls.Should().BeEmpty();
        _unitOfWork.SaveCalls.Should().Be(0);
    }

    [Fact]
    public async Task Update_WhenAuditThrows_PropagatesAndDoesNotSave()
    {
        _profiles.UpdateResult = AdminAccountDetailsUpdateResult.Updated(
            CreateProfile(personId: 5007, organizationId: 10, classCode: "1E"),
            ["classCode"]);
        _profiles.DetailsByPersonId[5007] = CreateProfile(personId: 5007, organizationId: 10);
        _accounts.AccountsByPersonId[5007] = new EducationAccountLookupSummary(1007, 5007, "EA-1007", "SGD", "ACTIVE", 10m);
        _audit.ExceptionToThrow = new InvalidOperationException("audit failed");
        UpdateAdminAccountDetailsHandler handler = CreateUpdateHandler();

        Func<Task> act = () => handler.Handle(
            new UpdateAdminAccountDetailsCommand(
                5007,
                "1E",
                "Preferred address",
                "student@example.sg",
                "+65 8123 4567",
                new DateTime(2026, 6, 22, 7, 0, 0, DateTimeKind.Utc)),
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("audit failed");
        _unitOfWork.SaveCalls.Should().Be(0);
    }

    private GetAdminAccountDetailsHandler CreateGetHandler()
        => new(_profiles, _accounts, _adminAccess, _clock);

    private UpdateAdminAccountDetailsHandler CreateUpdateHandler()
        => new(_profiles, _accounts, _adminAccess, _clock, _unitOfWork, _audit);

    private const string LongAddress =
        "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA"
        + "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA"
        + "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA"
        + "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA"
        + "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA"
        + "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA"
        + "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA"
        + "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA"
        + "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA"
        + "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA"
        + "A";

    private static AdminAccountDetailsProfile CreateProfile(
        long personId,
        long? organizationId,
        string? classCode = "1A",
        string? preferredEmail = "student@example.sg")
        => new(
            personId,
            "ACTIVE",
            9001,
            "ACTIVE",
            "S****123A",
            "Student One",
            new DateOnly(2010, 1, 1),
            "SG",
            "Official address",
            "Preferred address",
            preferredEmail,
            "+65 8123 4567",
            organizationId,
            "SCH-1",
            "School One",
            "2026",
            "SEC_1",
            classCode,
            new DateTime(2026, 6, 22, 7, 0, 0, DateTimeKind.Utc));

    private sealed class FakeAdminAccountDetailsRepository : IAdminAccountDetailsRepository
    {
        public Dictionary<long, AdminAccountDetailsProfile> DetailsByPersonId { get; } = [];
        public AdminAccountDetailsUpdateResult? UpdateResult { get; set; }

        public Task<AdminAccountDetailsProfile?> GetAsync(long personId, DateOnly today, CancellationToken cancellationToken)
            => Task.FromResult(DetailsByPersonId.GetValueOrDefault(personId));

        public Task<AdminAccountDetailsUpdateResult> UpdateAsync(
            long personId,
            string? classCode,
            string? preferredAddress,
            string? preferredEmail,
            string? preferredMobile,
            DateTime? expectedUpdatedAtUtc,
            DateTime utcNow,
            DateOnly today,
            CancellationToken cancellationToken)
            => Task.FromResult(UpdateResult ?? AdminAccountDetailsUpdateResult.NotFound());
    }

    private sealed class FakeEducationAccountLookupGateway : IEducationAccountLookupGateway
    {
        public Dictionary<long, EducationAccountLookupSummary> AccountsByPersonId { get; } = [];

        public Task<EducationAccountLookupSummary?> FindByPersonIdAsync(long personId, CancellationToken cancellationToken)
            => Task.FromResult(AccountsByPersonId.GetValueOrDefault(personId));
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

    private sealed class FakeCurrentUser : ICurrentUser
    {
        public long? UserAccountId => 42;
        public long? PersonId => null;
        public long? OrganizationUnitId => 10;
        public IReadOnlyCollection<long> OrganizationUnitIds { get; } = [10];
        public IReadOnlyCollection<string> Roles { get; } = ["SCHOOL_ADMIN"];
        public IReadOnlyCollection<string> Permissions { get; } = [];
        public string Portal => "AdminPortal";
        public bool IsAuthenticated => true;
        public bool HasPermission(string permission) => Permissions.Contains(permission);
    }

    private sealed class TestClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow { get; } = utcNow;

        public DateOnly TodayInSingapore() => SingaporeBusinessDay.FromUtc(UtcNow);
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

    private sealed class FakeAuditService : IAuditService
    {
        public List<AuditCall> Calls { get; } = [];
        public List<SchoolAuditContext> SchoolCalls { get; } = [];
        public Exception? ExceptionToThrow { get; set; }

        public Task RecordAsync(
            string actionCode,
            string entityTypeCode,
            string entityId,
            string? detailsJson = null,
            CancellationToken cancellationToken = default)
        {
            if (ExceptionToThrow is not null)
            {
                throw ExceptionToThrow;
            }

            Calls.Add(new AuditCall(actionCode, entityTypeCode, entityId, detailsJson));
            return Task.CompletedTask;
        }

        public Task RecordSchoolActionAsync(
            SchoolAuditContext context,
            CancellationToken cancellationToken = default)
        {
            if (ExceptionToThrow is not null)
            {
                throw ExceptionToThrow;
            }

            SchoolCalls.Add(context);
            return Task.CompletedTask;
        }
    }

    private sealed record AuditCall(string ActionCode, string EntityTypeCode, string EntityId, string? DetailsJson);
}
