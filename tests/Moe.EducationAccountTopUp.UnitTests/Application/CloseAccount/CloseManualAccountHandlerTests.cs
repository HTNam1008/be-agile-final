using System.Reflection;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moe.Application.Abstractions.Audit;
using Moe.Application.Abstractions.Clock;
using Moe.Application.Abstractions.Persistence;
using Moe.Application.Abstractions.Security;
using Moe.Modules.EducationAccountTopUp.Application.CloseAccount;
using Moe.Modules.EducationAccountTopUp.Domain.EducationAccounts;
using Moe.Modules.EducationAccountTopUp.IGateway.Repositories;
using Moe.Modules.IdentityPlatform.IGateway.People;
using Moe.Modules.IdentityPlatform.IGateway.Students;
using Moe.Modules.MailDelivery.IGateway;
using Moe.Modules.Notifications.IGateway.Notifications;
using Moe.SharedKernel.Results;
using Xunit;

namespace Moe.EducationAccountTopUp.UnitTests.Application.CloseAccount;

public sealed class CloseManualAccountHandlerTests
{
    private readonly FakeEducationAccountRepository _educationAccounts = new();
    private readonly FakePersonDirectory _people = new();
    private readonly FakeCurrentUser _currentUser = new();
    private readonly FakeAdminAccessControl _adminAccess = new();
    private readonly TestClock _clock = new(new DateTimeOffset(2026, 6, 22, 8, 0, 0, TimeSpan.Zero));
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeAccountHoldRepository _accountHolds = new();
    private readonly FakeAuditService _audit = new();
    private readonly FakeEmailDeliveryGateway _mailGateway = new();
    private readonly FakeStudentNotificationRecipientResolver _notificationRecipients = new();
    private readonly FakeNotificationWriter _notificationWriter = new();

    [Fact]
    public async Task Handle_OnSuccess_CallsAuditServiceWithReasonAndActor()
    {
        EducationAccount account = AddAccount(1001, personId: 5001);
        _people.OrganizationByPersonId[5001] = 10;
        CloseManualAccountHandler handler = CreateHandler();

        var result = await handler.Handle(CreateCommand(account.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        account.StatusCode.Should().Be(AccountStatuses.Closed);
        account.ClosingReasonCode.Should().Be(EducationAccountClosingReasonCodes.StudentIneligible);
        account.ClosedByLoginAccountId.Should().Be(42);
        _audit.Calls.Should().ContainSingle();
        AuditCall call = _audit.Calls.Single();
        call.ActionCode.Should().Be(AuditActionCodes.EducationAccountClosedManually);
        call.EntityTypeCode.Should().Be("EducationAccount");
        call.EntityId.Should().Be(account.Id.ToString());
        using JsonDocument document = JsonDocument.Parse(call.DetailsJson!);
        JsonElement root = document.RootElement;
        root.EnumerateObject().Select(x => x.Name).Should().BeEquivalentTo("reasonCode", "closedByLoginAccountId");
        root.GetProperty("reasonCode").GetString().Should().Be(EducationAccountClosingReasonCodes.StudentIneligible);
        root.GetProperty("closedByLoginAccountId").GetInt64().Should().Be(42);
        root.TryGetProperty("remarks", out _).Should().BeFalse();
        _unitOfWork.SaveCalls.Should().Be(1);
        _mailGateway.Messages.Should().ContainSingle();
        _mailGateway.Messages.Single().ToEmail.Should().Be("student.real@example.com");
        _mailGateway.Messages.Single().Subject.Should().Be("Your Education Account Has Been Closed");
    }

    [Fact]
    public async Task Handle_DoesNotDisablePerson_WhenClosingAccount()
    {
        EducationAccount account = AddAccount(1007, personId: 5007);
        _people.OrganizationByPersonId[5007] = 10;
        CloseManualAccountHandler handler = CreateHandler();

        var result = await handler.Handle(CreateCommand(account.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        account.StatusCode.Should().Be(AccountStatuses.Closed);
        _audit.Calls.Should().ContainSingle();
        _unitOfWork.SaveCalls.Should().Be(1);
    }

    [Fact]
    public async Task Handle_WhenMailDeliveryDisabled_StillClosesAccountAndSkipsEmail()
    {
        EducationAccount account = AddAccount(1008, personId: 5008);
        _people.OrganizationByPersonId[5008] = 10;
        CloseManualAccountHandler handler = CreateHandler(
            new ThrowingEmailRecipientResolver(),
            new TestDoubles.FixedEmailDeliverySwitch(isEnabled: false));

        var result = await handler.Handle(CreateCommand(account.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        account.StatusCode.Should().Be(AccountStatuses.Closed);
        _audit.Calls.Should().ContainSingle();
        _unitOfWork.SaveCalls.Should().Be(1);
        _mailGateway.Messages.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_OnAccountNotFound_DoesNotCallAuditService()
    {
        CloseManualAccountHandler handler = CreateHandler();

        var result = await handler.Handle(CreateCommand(9999), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(EducationAccountErrors.NotFound);
        _audit.Calls.Should().BeEmpty();
        _unitOfWork.SaveCalls.Should().Be(0);
    }

    [Fact]
    public async Task Handle_OnOutOfScopeOrganization_DoesNotCallAuditServiceOrCloseAccount()
    {
        EducationAccount account = AddAccount(1002, personId: 5002);
        _people.OrganizationByPersonId[5002] = 20;
        _adminAccess.AccessibleOrganizationIds.Clear();
        _adminAccess.AccessibleOrganizationIds.Add(10);
        CloseManualAccountHandler handler = CreateHandler();

        var result = await handler.Handle(CreateCommand(account.Id), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("AUTH.ORGANIZATION_OUTSIDE_SCOPE");
        account.StatusCode.Should().Be(AccountStatuses.Active);
        _audit.Calls.Should().BeEmpty();
        _unitOfWork.SaveCalls.Should().Be(0);
    }

    [Fact]
    public async Task Handle_OnAccountHolderWithNoOrganization_SchoolAdminDenied()
    {
        EducationAccount account = AddAccount(1003, personId: 5003);
        _people.OrganizationByPersonId[5003] = null;
        CloseManualAccountHandler handler = CreateHandler();

        var result = await handler.Handle(CreateCommand(account.Id), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("AUTH.ORGANIZATION_OUTSIDE_SCOPE");
        account.StatusCode.Should().Be(AccountStatuses.Active);
        _audit.Calls.Should().BeEmpty();
        _unitOfWork.SaveCalls.Should().Be(0);
    }

    [Fact]
    public async Task Handle_OnAccountHolderWithNoOrganization_HqAdminAllowed()
    {
        EducationAccount account = AddAccount(1004, personId: 5004);
        _people.OrganizationByPersonId[5004] = null;
        _adminAccess.IsHqAdmin = true;
        CloseManualAccountHandler handler = CreateHandler();

        var result = await handler.Handle(CreateCommand(account.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        account.StatusCode.Should().Be(AccountStatuses.Closed);
        _audit.Calls.Should().ContainSingle();
        _unitOfWork.SaveCalls.Should().Be(1);
    }

    [Fact]
    public async Task Handle_OnAlreadyClosedAccount_DoesNotCallAuditService()
    {
        EducationAccount account = AddAccount(1005, personId: 5005);
        _people.OrganizationByPersonId[5005] = 10;
        account.CloseManual(_clock.UtcNow, EducationAccountClosingReasonCodes.AdminError, "Already closed", 42)
            .IsSuccess.Should().BeTrue();
        CloseManualAccountHandler handler = CreateHandler();

        var result = await handler.Handle(CreateCommand(account.Id), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(AccountErrors.AlreadyClosed);
        _audit.Calls.Should().BeEmpty();
        _unitOfWork.SaveCalls.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WhenPendingHoldExists_ReturnsPendingPaymentInProgressAndLeavesAccountOpen()
    {
        EducationAccount account = AddAccount(1008, personId: 5008);
        _people.OrganizationByPersonId[5008] = 10;
        _accountHolds.PendingHoldAccountIds.Add(account.Id);
        CloseManualAccountHandler handler = CreateHandler();

        var result = await handler.Handle(CreateCommand(account.Id), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(EducationAccountErrors.PendingPaymentInProgress);
        account.StatusCode.Should().Be(AccountStatuses.Active);
        _audit.Calls.Should().BeEmpty();
        _unitOfWork.SaveCalls.Should().Be(0);
        _mailGateway.Messages.Should().BeEmpty();
        _accountHolds.CheckedAccountIds.Should().ContainSingle().Which.Should().Be(account.Id);
        _accountHolds.CheckedUtcNow.Should().ContainSingle().Which.Should().Be(_clock.UtcNow.UtcDateTime);
    }

    [Fact]
    public async Task Handle_WhenAuditServiceThrows_PropagatesAndDoesNotSwallow()
    {
        EducationAccount account = AddAccount(1006, personId: 5006);
        _people.OrganizationByPersonId[5006] = 10;
        _audit.ExceptionToThrow = new InvalidOperationException("audit failed");
        CloseManualAccountHandler handler = CreateHandler();

        Func<Task> act = () => handler.Handle(CreateCommand(account.Id), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("audit failed");
        _unitOfWork.SaveCalls.Should().Be(0);
    }

    private EducationAccount AddAccount(long accountId, long personId)
    {
        EducationAccount account = EducationAccount.OpenManual(
            personId,
            $"EA-{accountId}",
            _clock.UtcNow,
            "EXCEPTION",
            "Manual account",
            99).Value;

        FakeEducationAccountRepository.SetId(account, accountId);
        _educationAccounts.Accounts[accountId] = account;
        return account;
    }

    private CloseManualAccountHandler CreateHandler(
        IEmailRecipientResolver? recipientResolver = null,
        IEmailDeliverySwitch? mailSwitch = null)
        => new(
            _educationAccounts,
            _accountHolds,
            _people,
            _currentUser,
            _adminAccess,
            _clock,
            _unitOfWork,
            _audit,
            new EducationAccountClosureEmailService(
                _people,
                recipientResolver ?? new TestDoubles.FixedEmailRecipientResolver(),
                _mailGateway,
                mailSwitch ?? new TestDoubles.FixedEmailDeliverySwitch(),
                NullLogger<EducationAccountClosureEmailService>.Instance),
            _notificationRecipients,
            _notificationWriter);

    private static CloseManualAccountCommand CreateCommand(long educationAccountId)
        => new(
            educationAccountId,
            EducationAccountClosingReasonCodes.StudentIneligible,
            "Student no longer eligible");

    private sealed class FakeEducationAccountRepository : IEducationAccountRepository
    {
        private static readonly PropertyInfo IdProperty = typeof(EducationAccount)
            .GetProperty(nameof(EducationAccount.Id))!;

        public Dictionary<long, EducationAccount> Accounts { get; } = [];

        public static void SetId(EducationAccount account, long id) => IdProperty.SetValue(account, id);

        public Task<EducationAccount?> FindByIdAsync(long educationAccountId, CancellationToken cancellationToken)
            => Task.FromResult(Accounts.GetValueOrDefault(educationAccountId));

        public Task<EducationAccount?> FindByPersonIdAsync(long personId, CancellationToken cancellationToken)
            => Task.FromResult(Accounts.Values.SingleOrDefault(x => x.PersonId == personId));

        public Task<IReadOnlyCollection<EducationAccount>> ListActiveAsync(CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyCollection<EducationAccount>>(
                Accounts.Values.Where(x => x.StatusCode == AccountStatuses.Active).ToArray());

        public Task<bool> ExistsForPersonAsync(long personId, CancellationToken cancellationToken)
            => Task.FromResult(Accounts.Values.Any(x => x.PersonId == personId));

        public Task AddAsync(EducationAccount account, CancellationToken cancellationToken)
        {
            Accounts[account.Id] = account;
            return Task.CompletedTask;
        }
    }

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
                "Test Person",
                new DateOnly(2010, 1, 1),
                "SG",
                "CITIZEN",
                organizationId));
        }
    }

    private sealed class FakeAccountHoldRepository : IAccountHoldRepository
    {
        public HashSet<long> PendingHoldAccountIds { get; } = [];
        public List<long> CheckedAccountIds { get; } = [];
        public List<DateTime> CheckedUtcNow { get; } = [];

        public Task<bool> HasPendingHoldAsync(
            long educationAccountId,
            DateTime utcNow,
            CancellationToken cancellationToken)
        {
            CheckedAccountIds.Add(educationAccountId);
            CheckedUtcNow.Add(utcNow);
            return Task.FromResult(PendingHoldAccountIds.Contains(educationAccountId));
        }
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
            SchoolCalls.Add(context);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeEmailDeliveryGateway : IEmailDeliveryGateway
    {
        public List<EmailDeliveryMessage> Messages { get; } = [];

        public Task<Result> SendAsync(
            EmailDeliveryMessage message,
            CancellationToken cancellationToken)
        {
            Messages.Add(message);
            return Task.FromResult(Result.Success());
        }
    }

    private sealed class ThrowingEmailRecipientResolver : IEmailRecipientResolver
    {
        public Task<EmailRecipient?> ResolveForPersonAsync(
            long personId,
            CancellationToken cancellationToken)
            => throw new InvalidOperationException("Recipient resolver should not be called when mail is disabled.");

        public EmailRecipient? ResolveProvided(string? providedEmail)
            => throw new InvalidOperationException("Recipient resolver should not be called when mail is disabled.");
    }

    private sealed class FakeStudentNotificationRecipientResolver : IStudentNotificationRecipientResolver
    {
        public Task<long?> FindUserAccountIdByPersonIdAsync(long personId, CancellationToken cancellationToken)
            => Task.FromResult<long?>(personId + 1000);
    }

    private sealed class FakeNotificationWriter : INotificationWriter
    {
        public Task<Result<long>> CreateAsync(NotificationCreateRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(Result<long>.Success(1));
    }

    private sealed record AuditCall(string ActionCode, string EntityTypeCode, string EntityId, string? DetailsJson);
}
