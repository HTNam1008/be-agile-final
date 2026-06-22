using System.Reflection;
using System.Text.Json;
using FluentAssertions;
using Moe.Application.Abstractions.Audit;
using Moe.Application.Abstractions.Clock;
using Moe.Application.Abstractions.Persistence;
using Moe.Application.Abstractions.Security;
using Moe.Modules.EducationAccountTopUp.Application.OpenAccount;
using Moe.Modules.EducationAccountTopUp.Domain.EducationAccounts;
using Moe.Modules.EducationAccountTopUp.IGateway.Repositories;
using Moe.Modules.IdentityPlatform.IGateway.People;
using Xunit;

namespace Moe.EducationAccountTopUp.UnitTests.Application.OpenAccount;

public sealed class OpenManualAccountHandlerTests
{
    private readonly FakeEducationAccountRepository _educationAccounts = new();
    private readonly FakePersonDirectory _people = new();
    private readonly FakeCurrentUser _currentUser = new(userAccountId: 42);
    private readonly TestClock _clock = new(new DateTimeOffset(2026, 6, 22, 6, 0, 0, TimeSpan.Zero));
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeAuditService _audit = new();

    [Fact]
    public async Task Handle_OnSuccessfulCreation_CallsAuditServiceWithCorrectActionCode()
    {
        OpenManualAccountHandler handler = CreateHandler();

        var result = await handler.Handle(CreateCommand(personId: 5001), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _audit.Calls.Should().ContainSingle();
        AuditCall call = _audit.Calls.Single();
        call.ActionCode.Should().Be(AuditActionCodes.EducationAccountCreatedManually);
        call.EntityTypeCode.Should().Be("EducationAccount");
        call.EntityId.Should().Be(result.Value.EducationAccountId.ToString());
        _unitOfWork.SaveCalls.Should().Be(1);
    }

    [Fact]
    public async Task Handle_OnSuccessfulCreation_DetailsJsonExcludesPii()
    {
        OpenManualAccountHandler handler = CreateHandler();

        var result = await handler.Handle(CreateCommand(personId: 5002), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        using JsonDocument document = JsonDocument.Parse(_audit.Calls.Single().DetailsJson!);
        JsonElement root = document.RootElement;
        root.EnumerateObject().Select(x => x.Name).Should().BeEquivalentTo(
            "personId",
            "accountNumber",
            "openedByUserId");
        root.GetProperty("personId").GetInt64().Should().Be(5002);
        root.GetProperty("accountNumber").GetString().Should().Be(result.Value.AccountNumber);
        root.GetProperty("openedByUserId").GetInt64().Should().Be(42);
        root.TryGetProperty("nric", out _).Should().BeFalse();
        root.TryGetProperty("name", out _).Should().BeFalse();
        root.TryGetProperty("displayName", out _).Should().BeFalse();
        root.TryGetProperty("address", out _).Should().BeFalse();
    }

    [Fact]
    public async Task Handle_OnDuplicateAccount_DoesNotCallAuditService()
    {
        _educationAccounts.DuplicatePersonIds.Add(5003);
        OpenManualAccountHandler handler = CreateHandler();

        var result = await handler.Handle(CreateCommand(personId: 5003), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        _audit.Calls.Should().BeEmpty();
        _unitOfWork.SaveCalls.Should().Be(0);
    }

    [Fact]
    public async Task Handle_OnPersonNotFound_DoesNotCallAuditService()
    {
        _people.MissingPersonIds.Add(5004);
        OpenManualAccountHandler handler = CreateHandler();

        var result = await handler.Handle(CreateCommand(personId: 5004), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        _audit.Calls.Should().BeEmpty();
        _unitOfWork.SaveCalls.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WhenAuditServiceThrows_PropagatesAndDoesNotSwallow()
    {
        _audit.ExceptionToThrow = new InvalidOperationException("audit failed");
        OpenManualAccountHandler handler = CreateHandler();

        Func<Task> act = () => handler.Handle(CreateCommand(personId: 5005), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("audit failed");
        _unitOfWork.SaveCalls.Should().Be(0);
    }

    private OpenManualAccountHandler CreateHandler()
    {
        return new OpenManualAccountHandler(
            _educationAccounts,
            _people,
            _currentUser,
            _clock,
            _unitOfWork,
            _audit);
    }

    private static OpenManualAccountCommand CreateCommand(long personId)
        => new(personId, "MANUAL_OPEN", "Manual opening for test");

    private sealed class FakeEducationAccountRepository : IEducationAccountRepository
    {
        private static readonly PropertyInfo IdProperty = typeof(EducationAccount)
            .GetProperty(nameof(EducationAccount.Id))!;

        private long _nextId = 1000;

        public HashSet<long> DuplicatePersonIds { get; } = [];
        public List<EducationAccount> AddedAccounts { get; } = [];

        public Task<EducationAccount?> FindByPersonIdAsync(long personId, CancellationToken cancellationToken)
            => Task.FromResult<EducationAccount?>(AddedAccounts.SingleOrDefault(x => x.PersonId == personId));

        public Task<bool> ExistsForPersonAsync(long personId, CancellationToken cancellationToken)
            => Task.FromResult(DuplicatePersonIds.Contains(personId));

        public Task AddAsync(EducationAccount account, CancellationToken cancellationToken)
        {
            IdProperty.SetValue(account, _nextId++);
            AddedAccounts.Add(account);
            return Task.CompletedTask;
        }
    }

    private sealed class FakePersonDirectory : IPersonDirectory
    {
        public HashSet<long> MissingPersonIds { get; } = [];

        public Task<PersonSummary?> FindAsync(long personId, CancellationToken cancellationToken)
        {
            PersonSummary? summary = MissingPersonIds.Contains(personId)
                ? null
                : new PersonSummary(personId, "Test Student", new DateOnly(2010, 1, 1), "SG", "CITIZEN");

            return Task.FromResult(summary);
        }
    }

    private sealed class FakeCurrentUser(long? userAccountId) : ICurrentUser
    {
        private int _userAccountIdReads;

        public long? UserAccountId
        {
            get
            {
                _userAccountIdReads++;
                if (_userAccountIdReads > 1)
                {
                    throw new InvalidOperationException("UserAccountId was resolved more than once.");
                }

                return userAccountId;
            }
        }

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
    }

    private sealed record AuditCall(
        string ActionCode,
        string EntityTypeCode,
        string EntityId,
        string? DetailsJson);
}
