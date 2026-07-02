using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moe.Application.Abstractions.Clock;
using Moe.Application.Abstractions.Persistence;
using Moe.Modules.EducationAccountTopUp.Application.SettlementPreferences;
using Moe.Modules.EducationAccountTopUp.Domain.EducationAccounts;
using Moe.Modules.EducationAccountTopUp.IGateway.Repositories;
using Moe.Modules.IdentityPlatform.IGateway.Students;
using Moe.Modules.Notifications.IGateway.Notifications;
using Moe.SharedKernel.Results;
using Xunit;

namespace Moe.EducationAccountTopUp.UnitTests.Application.SettlementPreferences;

public sealed class SettlementPreferenceHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 30, 6, 0, 0, TimeSpan.Zero);

    private readonly FakeEducationAccountRepository _educationAccounts = new();
    private readonly FakeSettlementPreferenceRepository _settlementPreferences = new();
    private readonly FakeStudentNotificationRecipientResolver _notificationRecipients = new();
    private readonly FakeNotificationWriter _notificationWriter = new();
    private readonly TestClock _clock = new(Now);
    private readonly FakeUnitOfWork _unitOfWork = new();

    [Fact]
    public async Task Get_WhenStudentHasNoEducationAccount_ReturnsNotApplicable()
    {
        GetSettlementPreferenceHandler handler = CreateGetHandler();

        var result = await handler.Handle(new GetSettlementPreferenceQuery(9001), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.IsApplicable.Should().BeFalse();
        result.Value.Preference.Should().BeNull();
        result.Value.EmptyStateMessage.Should().Be("Available once your Education Account is opened");
    }

    [Fact]
    public async Task Set_WhenDestinationIsCpf_CreatesActiveCpfPreference()
    {
        EducationAccount account = AddAccount(personId: 9002, accountId: 7002);
        SetSettlementPreferenceHandler handler = CreateSetHandler();

        var result = await handler.Handle(
            new SetSettlementPreferenceCommand(
                account.PersonId,
                SettlementDestinationTypeCodes.Cpf,
                BankName: null,
                BankAccountNumber: null,
                ExpectedUpdatedAtUtc: null),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.IsApplicable.Should().BeTrue();
        result.Value.Preference.Should().NotBeNull();
        result.Value.Preference!.DestinationTypeCode.Should().Be(SettlementDestinationTypeCodes.Cpf);
        result.Value.Preference.DestinationMasked.Should().Be("CPF account (linked to NRIC)");
        result.Value.Preference.IsVerified.Should().BeTrue();
        _settlementPreferences.Added.Should().ContainSingle();
        _settlementPreferences.Added.Single().DestinationToken.Should().Be("CPF_DEFAULT");
        _settlementPreferences.Added.Single().IsActive.Should().BeTrue();
        _unitOfWork.SaveCalls.Should().Be(1);
    }

    [Fact]
    public async Task Set_WhenDestinationIsBank_CreatesMaskedBankPreference()
    {
        EducationAccount account = AddAccount(personId: 9003, accountId: 7003);
        SetSettlementPreferenceHandler handler = CreateSetHandler();

        var result = await handler.Handle(
            new SetSettlementPreferenceCommand(
                account.PersonId,
                SettlementDestinationTypeCodes.Bank,
                BankName: "DBS",
                BankAccountNumber: "123456789",
                ExpectedUpdatedAtUtc: null),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Preference!.DestinationTypeCode.Should().Be(SettlementDestinationTypeCodes.Bank);
        result.Value.Preference.DestinationMasked.Should().Be("DBS account ending 6789");
        _settlementPreferences.Added.Single().DestinationToken.Should()
            .Contain("\"bankName\":\"DBS\"")
            .And.Contain("\"accountNumber\":\"123456789\"");
    }

    [Theory]
    [InlineData("", "123456789")]
    [InlineData("DBS", "12A456")]
    [InlineData("DBS", "12345")]
    public async Task Set_WhenBankDetailsAreInvalid_ReturnsValidationFailure(
        string bankName,
        string bankAccountNumber)
    {
        EducationAccount account = AddAccount(personId: 9004, accountId: 7004);
        SetSettlementPreferenceHandler handler = CreateSetHandler();

        var result = await handler.Handle(
            new SetSettlementPreferenceCommand(
                account.PersonId,
                SettlementDestinationTypeCodes.Bank,
                bankName,
                bankAccountNumber,
                ExpectedUpdatedAtUtc: null),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(EducationAccountErrors.InvalidSettlementPreference);
        _settlementPreferences.Added.Should().BeEmpty();
        _unitOfWork.SaveCalls.Should().Be(0);
    }

    [Fact]
    public async Task Set_WhenChangingPreference_DeactivatesPreviousActiveAndCreatesNewRow()
    {
        EducationAccount account = AddAccount(personId: 9005, accountId: 7005);
        SettlementPreference current = SettlementPreference.Create(
            account.Id,
            SettlementDestinationTypeCodes.Cpf,
            "CPF_DEFAULT",
            "CPF account (linked to NRIC)",
            isVerified: true,
            Now.AddMinutes(-5).UtcDateTime);
        _settlementPreferences.ActiveByAccount[account.Id] = current;
        SetSettlementPreferenceHandler handler = CreateSetHandler();

        var result = await handler.Handle(
            new SetSettlementPreferenceCommand(
                account.PersonId,
                SettlementDestinationTypeCodes.Bank,
                BankName: "OCBC",
                BankAccountNumber: "987654321",
                ExpectedUpdatedAtUtc: current.UpdatedAtUtc),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        current.IsActive.Should().BeFalse();
        _settlementPreferences.Added.Should().ContainSingle();
        _settlementPreferences.Added.Single().IsActive.Should().BeTrue();
        _settlementPreferences.Added.Single().DestinationTypeCode.Should().Be(SettlementDestinationTypeCodes.Bank);
        _unitOfWork.SaveCalls.Should().Be(1);
    }

    [Fact]
    public async Task Set_WhenExpectedUpdatedAtIsStale_ReturnsConflictAndDoesNotSave()
    {
        EducationAccount account = AddAccount(personId: 9006, accountId: 7006);
        SettlementPreference current = SettlementPreference.Create(
            account.Id,
            SettlementDestinationTypeCodes.Cpf,
            "CPF_DEFAULT",
            "CPF account (linked to NRIC)",
            isVerified: true,
            Now.UtcDateTime);
        _settlementPreferences.ActiveByAccount[account.Id] = current;
        SetSettlementPreferenceHandler handler = CreateSetHandler();

        var result = await handler.Handle(
            new SetSettlementPreferenceCommand(
                account.PersonId,
                SettlementDestinationTypeCodes.Bank,
                BankName: "UOB",
                BankAccountNumber: "76543210",
                ExpectedUpdatedAtUtc: Now.AddMinutes(-1).UtcDateTime),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(EducationAccountErrors.SettlementPreferenceConflict);
        current.IsActive.Should().BeTrue();
        _settlementPreferences.Added.Should().BeEmpty();
        _unitOfWork.SaveCalls.Should().Be(0);
    }

    [Fact]
    public async Task Set_WhenStudentHasNoEducationAccount_ReturnsNotApplicableWithoutSaving()
    {
        SetSettlementPreferenceHandler handler = CreateSetHandler();

        var result = await handler.Handle(
            new SetSettlementPreferenceCommand(
                PersonId: 9999,
                DestinationTypeCode: SettlementDestinationTypeCodes.Cpf,
                BankName: null,
                BankAccountNumber: null,
                ExpectedUpdatedAtUtc: null),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.IsApplicable.Should().BeFalse();
        result.Value.Preference.Should().BeNull();
        _settlementPreferences.Added.Should().BeEmpty();
        _unitOfWork.SaveCalls.Should().Be(0);
    }

    private GetSettlementPreferenceHandler CreateGetHandler()
        => new(_educationAccounts, _settlementPreferences);

    private SetSettlementPreferenceHandler CreateSetHandler()
        => new(
            _educationAccounts,
            _settlementPreferences,
            _notificationRecipients,
            _notificationWriter,
            _clock,
            _unitOfWork,
            NullLogger<SetSettlementPreferenceHandler>.Instance);

    private EducationAccount AddAccount(long personId, long accountId)
    {
        EducationAccount account = EducationAccount.OpenManual(
            personId,
            $"EA-SETTLE-{personId}",
            Now,
            "TEST",
            "Settlement preference test account",
            openedBy: 42).Value;
        typeof(EducationAccount).GetProperty(nameof(EducationAccount.Id))!.SetValue(account, accountId);
        _educationAccounts.Accounts[personId] = account;
        return account;
    }

    private sealed class FakeEducationAccountRepository : IEducationAccountRepository
    {
        public Dictionary<long, EducationAccount> Accounts { get; } = [];

        public Task<EducationAccount?> FindByIdAsync(long educationAccountId, CancellationToken cancellationToken)
            => Task.FromResult(Accounts.Values.SingleOrDefault(x => x.Id == educationAccountId));

        public Task<EducationAccount?> FindByPersonIdAsync(long personId, CancellationToken cancellationToken)
            => Task.FromResult(Accounts.GetValueOrDefault(personId));

        public Task<IReadOnlyCollection<EducationAccount>> ListActiveAsync(CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyCollection<EducationAccount>>(Accounts.Values.ToArray());

        public Task<bool> ExistsForPersonAsync(long personId, CancellationToken cancellationToken)
            => Task.FromResult(Accounts.ContainsKey(personId));

        public Task AddAsync(EducationAccount account, CancellationToken cancellationToken)
        {
            Accounts[account.PersonId] = account;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeSettlementPreferenceRepository : ISettlementPreferenceRepository
    {
        public Dictionary<long, SettlementPreference> ActiveByAccount { get; } = [];
        public List<SettlementPreference> Added { get; } = [];

        public Task<SettlementPreference?> FindActiveByEducationAccountIdAsync(
            long educationAccountId,
            CancellationToken cancellationToken)
            => Task.FromResult(ActiveByAccount.GetValueOrDefault(educationAccountId));

        public Task AddAsync(SettlementPreference preference, CancellationToken cancellationToken)
        {
            Added.Add(preference);
            ActiveByAccount[preference.EducationAccountId] = preference;
            return Task.CompletedTask;
        }
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
}
