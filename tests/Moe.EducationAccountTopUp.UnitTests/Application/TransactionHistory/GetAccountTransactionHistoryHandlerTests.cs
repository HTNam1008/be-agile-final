using System.Reflection;
using FluentAssertions;
using Moe.Application.Abstractions.Security;
using Moe.Modules.EducationAccountTopUp.Application.TransactionHistory;
using Moe.Modules.EducationAccountTopUp.Domain.EducationAccounts;
using Moe.Modules.EducationAccountTopUp.IGateway.History;
using Moe.Modules.EducationAccountTopUp.IGateway.Repositories;
using Moe.Modules.IdentityPlatform.IGateway.Accounts;
using Moe.Modules.IdentityPlatform.IGateway.People;
using Moe.SharedKernel.Results;
using Xunit;

namespace Moe.EducationAccountTopUp.UnitTests.Application.TransactionHistory;

public sealed class GetAccountTransactionHistoryHandlerTests
{
    private readonly FakeEducationAccountRepository _educationAccounts = new();
    private readonly FakePersonDirectory _people = new();
    private readonly FakeAdminAccessControl _adminAccess = new();
    private readonly FakeTransactionHistoryReader _reader = new();
    private readonly FakeLoginAccountDisplayDirectory _displayNames = new();

    [Fact]
    public async Task Handle_OnHqAdminWithNoOrganization_ReturnsTransactionsAndResolvesActorsInOneBulkCall()
    {
        EducationAccount account = AddAccount(1001, personId: 5001);
        _people.OrganizationByPersonId[5001] = null;
        _adminAccess.IsHqAdmin = true;
        _reader.Page = new HistoryPage<AccountTransactionHistoryProjection>(
            [
                new AccountTransactionHistoryProjection(
                    11,
                    new DateTime(2026, 6, 22, 8, 0, 0, DateTimeKind.Utc),
                    "CREDIT",
                    25m,
                    "TOPUP",
                    "Monthly grant",
                    125m,
                    null),
                new AccountTransactionHistoryProjection(
                    10,
                    new DateTime(2026, 6, 21, 8, 0, 0, DateTimeKind.Utc),
                    "ADJUST",
                    -5m,
                    "MANUAL_ADJUSTMENT",
                    "Correction",
                    100m,
                    42),
                new AccountTransactionHistoryProjection(
                    9,
                    new DateTime(2026, 6, 20, 8, 0, 0, DateTimeKind.Utc),
                    "ADJUST",
                    10m,
                    "MANUAL_ADJUSTMENT",
                    "Correction",
                    105m,
                    42)
            ],
            3);
        _displayNames.Names[42] = "Admin One";
        GetAccountTransactionHistoryHandler handler = CreateHandler();

        var result = await handler.Handle(
            new GetAccountTransactionHistoryQuery(account.Id, Page: 1, PageSize: 20),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.TotalCount.Should().Be(3);
        result.Value.Items.Select(x => x.PerformedBy).Should().Equal("System", "Admin One", "Admin One");
        result.Value.Items[0].TypeLabel.Should().Be("Top-up");
        result.Value.Items[1].TypeLabel.Should().Be("MANUAL_ADJUSTMENT");
        _displayNames.Requests.Should().ContainSingle()
            .Which.Should().Equal(42);
    }

    [Fact]
    public async Task Handle_OnSchoolAdminInScope_ReturnsTransactions()
    {
        EducationAccount account = AddAccount(1004, personId: 5004);
        _people.OrganizationByPersonId[5004] = 10;
        _reader.Page = new HistoryPage<AccountTransactionHistoryProjection>(
            [
                new AccountTransactionHistoryProjection(
                    12,
                    new DateTime(2026, 6, 22, 9, 0, 0, DateTimeKind.Utc),
                    "CREDIT",
                    30m,
                    "TOPUP",
                    "Monthly grant",
                    130m,
                    null)
            ],
            1);
        GetAccountTransactionHistoryHandler handler = CreateHandler();

        var result = await handler.Handle(
            new GetAccountTransactionHistoryQuery(account.Id, Page: 1, PageSize: 20),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().ContainSingle()
            .Which.PerformedBy.Should().Be("System");
        _reader.Calls.Should().Be(1);
    }

    [Fact]
    public async Task Handle_OnSchoolAdminWithNoOrganization_DeniesBeforeReadingLedger()
    {
        EducationAccount account = AddAccount(1002, personId: 5002);
        _people.OrganizationByPersonId[5002] = null;
        GetAccountTransactionHistoryHandler handler = CreateHandler();

        var result = await handler.Handle(
            new GetAccountTransactionHistoryQuery(account.Id, Page: 1, PageSize: 20),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("AUTH.ORGANIZATION_OUTSIDE_SCOPE");
        _reader.Calls.Should().Be(0);
        _displayNames.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_OnOutOfScopeSchoolAdmin_DeniesBeforeReadingLedger()
    {
        EducationAccount account = AddAccount(1003, personId: 5003);
        _people.OrganizationByPersonId[5003] = 20;
        _adminAccess.AccessibleOrganizationIds.Clear();
        _adminAccess.AccessibleOrganizationIds.Add(10);
        GetAccountTransactionHistoryHandler handler = CreateHandler();

        var result = await handler.Handle(
            new GetAccountTransactionHistoryQuery(account.Id, Page: 1, PageSize: 20),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("AUTH.ORGANIZATION_OUTSIDE_SCOPE");
        _reader.Calls.Should().Be(0);
    }

    [Fact]
    public async Task Handle_OnAccountNotFound_ReturnsNotFound()
    {
        GetAccountTransactionHistoryHandler handler = CreateHandler();

        var result = await handler.Handle(
            new GetAccountTransactionHistoryQuery(9999, Page: 1, PageSize: 20),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(EducationAccountErrors.NotFound);
        _reader.Calls.Should().Be(0);
    }

    private EducationAccount AddAccount(long accountId, long personId)
    {
        EducationAccount account = EducationAccount.OpenManual(
            personId,
            $"EA-{accountId}",
            DateTimeOffset.UtcNow,
            "EXCEPTION",
            "Manual account",
            99).Value;

        FakeEducationAccountRepository.SetId(account, accountId);
        _educationAccounts.Accounts[accountId] = account;
        return account;
    }

    private GetAccountTransactionHistoryHandler CreateHandler()
        => new(
            _educationAccounts,
            _people,
            _adminAccess,
            _reader,
            _displayNames);

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

    private sealed class FakeTransactionHistoryReader : IAccountTransactionHistoryReader
    {
        public int Calls { get; private set; }
        public HistoryPage<AccountTransactionHistoryProjection> Page { get; set; } = new([], 0);

        public Task<HistoryPage<AccountTransactionHistoryProjection>> GetTransactionsAsync(
            long educationAccountId,
            int page,
            int pageSize,
            CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(Page);
        }
    }

    private sealed class FakeLoginAccountDisplayDirectory : ILoginAccountDisplayDirectory
    {
        public Dictionary<long, string> Names { get; } = [];
        public List<IReadOnlyCollection<long>> Requests { get; } = [];

        public Task<IReadOnlyDictionary<long, string>> FindDisplayNamesAsync(
            IReadOnlyCollection<long> loginAccountIds,
            CancellationToken cancellationToken)
        {
            Requests.Add(loginAccountIds.ToArray());
            return Task.FromResult<IReadOnlyDictionary<long, string>>(
                Names.Where(x => loginAccountIds.Contains(x.Key)).ToDictionary());
        }
    }
}
