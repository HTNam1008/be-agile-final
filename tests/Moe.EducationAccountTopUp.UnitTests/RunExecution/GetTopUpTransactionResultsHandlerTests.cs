using FluentAssertions;
using Moe.Application.Abstractions.Security;
using Moe.Infrastructure.Shared.Api;
using Moe.Modules.EducationAccountTopUp.Application.History;
using Moe.Modules.EducationAccountTopUp.Application.RunExecution.TransactionResults;
using Moe.Modules.EducationAccountTopUp.Contracts.TopUps;
using Moe.Modules.EducationAccountTopUp.Domain.TopUps;
using Moe.Modules.EducationAccountTopUp.IGateway.RunSummary;
using Moe.Modules.EducationAccountTopUp.IGateway.TopUps;
using Moe.Modules.EducationAccountTopUp.IGateway.TransactionResults;
using Moe.Modules.IdentityPlatform.IGateway.Students;
using Moe.Modules.IdentityPlatform.IGateway.Students.TopUpSearch;
using Xunit;

namespace Moe.EducationAccountTopUp.UnitTests.RunExecution;

public sealed class GetTopUpTransactionResultsHandlerTests
{
    [Fact]
    public async Task Should_Return_Masked_Paged_Results_And_Scrub_Unknown_Reason()
    {
        FakeTransactionReader transactionReader = new(
            new TopUpTransactionResultProjection(
                501,
                101,
                0m,
                TopUpTransactionStatusCodes.Failed,
                "SQL timeout for NRIC S1234567A",
                null,
                DateTime.UtcNow,
                DateTime.UtcNow));

        GetTopUpTransactionResultsHandler handler = CreateHandler(
            organizationId: 10,
            transactionReader,
            permissions: [TopUpPermissions.Manage],
            organizationIds: [10]);

        var result = await handler.Handle(
            new GetTopUpTransactionResultsQuery(
                42,
                new TopUpTransactionResultFilter(null, null, null, null, null),
                Page: 1,
                PageSize: 25),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        PageResponse<TopUpTransactionResultItem> page = result.Value;
        TopUpTransactionResultItem item = page.Items.Should().ContainSingle().Subject;
        item.MaskedAccountNumber.Should().Be("EA-****-0001");
        item.MaskedStudentNumber.Should().Be("*********0001");
        item.StudentDisplayName.Should().Be("Student One");
        item.Reason.Should().Be(SafeReasons.UnexpectedError);
        item.CurrencyCode.Should().Be("SGD");
    }

    [Fact]
    public async Task Should_Deny_Run_Outside_Organization_Scope()
    {
        GetTopUpTransactionResultsHandler handler = CreateHandler(
            organizationId: 20,
            new FakeTransactionReader(),
            permissions: [TopUpPermissions.Manage],
            organizationIds: [10]);

        var result = await handler.Handle(
            Query(),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(TopUpHistoryErrors.OrganizationOutsideScope);
    }

    [Fact]
    public async Task Should_Return_NotFound_When_Run_Does_Not_Exist()
    {
        GetTopUpTransactionResultsHandler handler = new(
            new FakeRunReader(null),
            new TopUpAccessScopeResolver(
                new FakeCurrentUser([TopUpPermissions.Manage], [10])),
            new FakeTransactionReader(),
            new FakeAccountRepository(),
            new FakeStudentDirectory());

        var result = await handler.Handle(Query(), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(
            Moe.Modules.EducationAccountTopUp.Domain.TopUps.TopUpErrors.RunNotFound);
    }

    [Fact]
    public async Task Search_Should_Restrict_Transaction_Query_To_Matching_Accounts()
    {
        FakeTransactionReader transactionReader = new();
        GetTopUpTransactionResultsHandler handler = CreateHandler(
            organizationId: 10,
            transactionReader,
            permissions: [TopUpPermissions.Manage],
            organizationIds: [10]);

        var result = await handler.Handle(
            new GetTopUpTransactionResultsQuery(
                42,
                new TopUpTransactionResultFilter(
                    null,
                    "Student One",
                    null,
                    null,
                    null),
                1,
                25),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        transactionReader.MatchingEducationAccountIds.Should().Equal(101);
    }

    private static GetTopUpTransactionResultsQuery Query()
        => new(
            42,
            new TopUpTransactionResultFilter(null, null, null, null, null),
            1,
            25);

    private static GetTopUpTransactionResultsHandler CreateHandler(
        long organizationId,
        FakeTransactionReader transactionReader,
        IReadOnlyCollection<string> permissions,
        IReadOnlyCollection<long> organizationIds)
        => new(
            new FakeRunReader(new RunSummaryProjection(
                42,
                7,
                organizationId,
                DateTime.UtcNow,
                "MANUAL",
                "COMPLETED",
                1,
                1,
                0,
                1,
                0,
                0m,
                DateTime.UtcNow,
                DateTime.UtcNow)),
            new TopUpAccessScopeResolver(
                new FakeCurrentUser(permissions, organizationIds)),
            transactionReader,
            new FakeAccountRepository(),
            new FakeStudentDirectory());

    private sealed class FakeRunReader(RunSummaryProjection? projection)
        : ITopUpRunSummaryReader
    {
        public Task<RunSummaryProjection?> GetByIdAsync(
            long runId,
            CancellationToken cancellationToken)
            => Task.FromResult(projection?.RunId == runId ? projection : null);
    }

    private sealed class FakeTransactionReader(
        params TopUpTransactionResultProjection[] items)
        : ITopUpTransactionResultsReader
    {
        public IReadOnlyCollection<long>? MatchingEducationAccountIds { get; private set; }

        public Task<TransactionResultsPage> GetPageAsync(
            long runId,
            TopUpTransactionResultFilter filter,
            IReadOnlyCollection<long>? matchingEducationAccountIds,
            int page,
            int pageSize,
            CancellationToken cancellationToken)
        {
            MatchingEducationAccountIds = matchingEducationAccountIds;
            return Task.FromResult(
                new TransactionResultsPage(items, items.LongLength));
        }
    }

    private sealed class FakeAccountRepository : ITopUpAccountProjectionRepository
    {
        private static readonly TopUpAccountProjection Account =
            new(1, 101, "EA-DEMO-0001", "ACTIVE", 100m);

        public Task<IReadOnlyCollection<long>> FindMatchingPersonIdsAsync(
            TopUpAccountSearchCriteria criteria,
            CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyCollection<long>>([1]);

        public Task<IReadOnlyDictionary<long, TopUpAccountProjection>> FindByPersonIdsAsync(
            IReadOnlyCollection<long> personIds,
            CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyDictionary<long, TopUpAccountProjection>>(
                personIds.Contains(1)
                    ? new Dictionary<long, TopUpAccountProjection> { [1] = Account }
                    : new Dictionary<long, TopUpAccountProjection>());

        public Task<IReadOnlyDictionary<long, TopUpAccountProjection>> FindByEducationAccountIdsAsync(
            IReadOnlyCollection<long> educationAccountIds,
            CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyDictionary<long, TopUpAccountProjection>>(
                educationAccountIds.Contains(101)
                    ? new Dictionary<long, TopUpAccountProjection> { [101] = Account }
                    : new Dictionary<long, TopUpAccountProjection>());
    }

    private sealed class FakeStudentDirectory : ITopUpStudentSearchDirectory
    {
        public Task<TopUpStudentSearchSummaryPage> SearchForTopUpAsync(
            TopUpStudentSearchCriteria criteria,
            IReadOnlyCollection<long> scopedOrganizationIds,
            CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<IReadOnlyCollection<long>> FindMatchingPersonIdsForTopUpAsync(
            TopUpStudentSearchCriteria criteria,
            IReadOnlyCollection<long> scopedOrganizationIds,
            CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyCollection<long>>([1]);

        public Task<IReadOnlyDictionary<long, TopUpStudentDisplaySummary>> FindDisplayByPersonIdsForTopUpAsync(
            IReadOnlyCollection<long> personIds,
            long organizationId,
            CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyDictionary<long, TopUpStudentDisplaySummary>>(
                personIds.Contains(1)
                    ? new Dictionary<long, TopUpStudentDisplaySummary>
                    {
                        [1] = new(1, "DEMO-STU-0001", "Student One")
                    }
                    : new Dictionary<long, TopUpStudentDisplaySummary>());

        public Task<IReadOnlyList<AccountTaxonomyLevel>> GetAccountTaxonomyAsync(
            IReadOnlyCollection<long> scopedOrganizationIds,
            CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<AccountTaxonomyLevel>>(Array.Empty<AccountTaxonomyLevel>());
    }

    private sealed class FakeCurrentUser(
        IReadOnlyCollection<string> permissions,
        IReadOnlyCollection<long> organizationIds) : ICurrentUser
    {
        public long? UserAccountId => 1;
        public long? PersonId => null;
        public long? OrganizationUnitId => organizationIds.FirstOrDefault();
        public IReadOnlyCollection<long> OrganizationUnitIds => organizationIds;
        public IReadOnlyCollection<string> Roles => ["SCHOOL_ADMIN"];
        public IReadOnlyCollection<string> Permissions => permissions;
        public string Portal => "ADMIN";
        public bool IsAuthenticated => true;
        public bool HasPermission(string permission) => permissions.Contains(permission);
    }
}
