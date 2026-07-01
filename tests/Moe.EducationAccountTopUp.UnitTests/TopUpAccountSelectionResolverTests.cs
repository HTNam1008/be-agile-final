using FluentAssertions;
using FluentValidation;
using Moe.Application.Abstractions.Security;
using Moe.Modules.EducationAccountTopUp.Application.TopUps;
using Moe.Modules.EducationAccountTopUp.Application.TopUps.AccountSelection;
using Moe.Modules.EducationAccountTopUp.Application.TopUps.Filters;
using Moe.Modules.EducationAccountTopUp.Contracts.TopUps.Enums;
using Moe.Modules.EducationAccountTopUp.Domain.TopUps;
using Moe.Modules.EducationAccountTopUp.IGateway.TopUps;
using Moe.Modules.IdentityPlatform.IGateway.Students;
using Moe.Modules.IdentityPlatform.IGateway.Students.TopUpSearch;
using Xunit;

namespace Moe.EducationAccountTopUp.UnitTests;

public sealed class TopUpAccountSelectionResolverTests
{
    [Fact]
    public async Task All_matching_filter_resolves_current_filter_minus_exclusions()
    {
        FakeAccountProjectionRepository accounts = new(
            Account(1, 101, "EA-001", "Active", 25),
            Account(2, 102, "EA-002", "Active", 50),
            Account(3, 103, "EA-003", "Active", 75));
        FakeTopUpStudentSearchDirectory students = new(
            Student(1, 10),
            Student(2, 10),
            Student(3, 20));
        TopUpAccountSelectionResolver resolver = CreateResolver(accounts, students, scopedOrganizationIds: [10]);

        TopUpAccountSelection selection = TopUpAccountSelection.AllMatching(
            new TopUpAccountFilter(
                Search: null,
                OrganizationId: 10,
                SchoolingStatusCode: null,
                LevelCode: null,
                ClassCode: null,
                AccountStatusCode: "Active",
                AgeFrom: null,
                AgeTo: null,
                BalanceFrom: null,
                BalanceTo: null),
            excludedEducationAccountIds: [102]);

        var result = await resolver.ResolveAsync(selection, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.EducationAccountIds.Should().Equal(101);
        result.Value.TotalMatched.Should().Be(2);
        result.Value.TotalExcluded.Should().Be(1);
        result.Value.TotalSelected.Should().Be(1);
    }

    [Fact]
    public async Task All_matching_filter_rejects_exclusions_outside_matching_scope()
    {
        FakeAccountProjectionRepository accounts = new(
            Account(1, 101, "EA-001", "Active", 25),
            Account(2, 102, "EA-002", "Active", 50),
            Account(3, 103, "EA-003", "Active", 75));
        FakeTopUpStudentSearchDirectory students = new(
            Student(1, 10),
            Student(2, 10),
            Student(3, 20));
        TopUpAccountSelectionResolver resolver = CreateResolver(accounts, students, scopedOrganizationIds: [10]);

        TopUpAccountSelection selection = TopUpAccountSelection.AllMatching(
            new TopUpAccountFilter(
                Search: null,
                OrganizationId: 10,
                SchoolingStatusCode: null,
                LevelCode: null,
                ClassCode: null,
                AccountStatusCode: "Active",
                AgeFrom: null,
                AgeTo: null,
                BalanceFrom: null,
                BalanceTo: null),
            excludedEducationAccountIds: [103]);

        var result = await resolver.ResolveAsync(selection, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(TopUpErrors.AccountSelectionOutsideScope);
    }

    [Fact]
    public async Task Explicit_selection_rejects_account_outside_admin_scope()
    {
        FakeAccountProjectionRepository accounts = new(
            Account(1, 101, "EA-001", "Active", 25),
            Account(3, 103, "EA-003", "Active", 75));
        FakeTopUpStudentSearchDirectory students = new(
            Student(1, 10),
            Student(3, 20));
        TopUpAccountSelectionResolver resolver = CreateResolver(accounts, students, scopedOrganizationIds: [10]);

        var result = await resolver.ResolveAsync(
            TopUpAccountSelection.Explicit([101, 103]),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(TopUpErrors.AccountSelectionOutsideScope);
    }

    [Fact]
    public void Validator_rejects_select_all_payload_with_selected_ids()
    {
        TopUpAccountSelectionValidator validator = new();
        TopUpAccountSelection selection = new(
            TopUpAccountSelectionMode.AllMatchingFilter,
            new TopUpAccountFilter(null, 10, null, null, null, null, null, null, null, null),
            SelectedEducationAccountIds: [101],
            ExcludedEducationAccountIds: []);

        var result = validator.Validate(selection);

        result.IsValid.Should().BeFalse();
    }

    private static TopUpAccountSelectionResolver CreateResolver(
        FakeAccountProjectionRepository accounts,
        FakeTopUpStudentSearchDirectory students,
        IReadOnlyCollection<long> scopedOrganizationIds)
        => new(
            new FakeAdminAccessControl(scopedOrganizationIds),
            new TopUpAccountSelectionValidator(),
            accounts,
            students);

    private static TopUpAccountProjection Account(
        long personId,
        long educationAccountId,
        string accountNumber,
        string accountStatusCode,
        decimal balance)
        => new(personId, educationAccountId, accountNumber, accountStatusCode, balance);

    private static StudentScope Student(long personId, long organizationId)
        => new(personId, organizationId);

    private sealed record StudentScope(long PersonId, long OrganizationId);

    private sealed class FakeAdminAccessControl(IReadOnlyCollection<long> organizationUnitIds) : IAdminAccessControl
    {
        public bool IsHqAdmin => false;
        public bool IsSchoolAdmin => true;
        public IReadOnlyCollection<long> ScopedOrganizationIds => organizationUnitIds;
        public bool CanAccessOrganization(long organizationId) => organizationUnitIds.Contains(organizationId);

        public Moe.SharedKernel.Results.Result EnsureCanAccessOrganization(long organizationId)
            => CanAccessOrganization(organizationId)
                ? Moe.SharedKernel.Results.Result.Success()
                : Moe.SharedKernel.Results.Result.Failure(TopUpErrors.OrganizationOutsideScope);

        public AdminOrganizationScope ResolveOrganizationFilter(long? requestedOrganizationId)
        {
            if (requestedOrganizationId is long requested)
            {
                return new AdminOrganizationScope(
                    organizationUnitIds.Contains(requested),
                    false,
                    requested,
                    organizationUnitIds);
            }

            return new AdminOrganizationScope(true, false, null, organizationUnitIds);
        }
    }

    private sealed class FakeAccountProjectionRepository(
        params TopUpAccountProjection[] accounts) : ITopUpAccountProjectionRepository
    {
        public Task<IReadOnlyCollection<long>> FindMatchingPersonIdsAsync(
            TopUpAccountSearchCriteria criteria,
            CancellationToken cancellationToken)
        {
            IEnumerable<TopUpAccountProjection> query = accounts;

            if (!string.IsNullOrWhiteSpace(criteria.Search))
            {
                query = query.Where(x => x.AccountNumber.Contains(criteria.Search, StringComparison.OrdinalIgnoreCase));
            }

            if (criteria.BalanceFrom.HasValue)
            {
                query = query.Where(x => x.Balance >= criteria.BalanceFrom.Value);
            }

            if (criteria.BalanceTo.HasValue)
            {
                query = query.Where(x => x.Balance <= criteria.BalanceTo.Value);
            }

            if (!string.IsNullOrWhiteSpace(criteria.AccountStatusCode))
            {
                query = query.Where(x => x.AccountStatusCode == criteria.AccountStatusCode);
            }

            IReadOnlyCollection<long> personIds = query.Select(x => x.PersonId).Distinct().ToArray();
            return Task.FromResult(personIds);
        }

        public Task<IReadOnlyDictionary<long, TopUpAccountProjection>> FindByPersonIdsAsync(
            IReadOnlyCollection<long> personIds,
            CancellationToken cancellationToken)
        {
            IReadOnlyDictionary<long, TopUpAccountProjection> result = accounts
                .Where(x => personIds.Contains(x.PersonId))
                .ToDictionary(x => x.PersonId);

            return Task.FromResult(result);
        }

        public Task<IReadOnlyDictionary<long, TopUpAccountProjection>> FindByEducationAccountIdsAsync(
            IReadOnlyCollection<long> educationAccountIds,
            CancellationToken cancellationToken)
        {
            IReadOnlyDictionary<long, TopUpAccountProjection> result = accounts
                .Where(x => educationAccountIds.Contains(x.EducationAccountId))
                .ToDictionary(x => x.EducationAccountId);

            return Task.FromResult(result);
        }
    }

    private sealed class FakeTopUpStudentSearchDirectory(
        params StudentScope[] students) : ITopUpStudentSearchDirectory
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
        {
            IEnumerable<StudentScope> query = students
                .Where(x => scopedOrganizationIds.Contains(x.OrganizationId));

            if (criteria.CandidatePersonIds is not null)
            {
                query = criteria.CandidatePersonIds.Count == 0
                    ? []
                    : query.Where(x => criteria.CandidatePersonIds.Contains(x.PersonId));
            }

            if (criteria.OrganizationId.HasValue)
            {
                query = query.Where(x => x.OrganizationId == criteria.OrganizationId.Value);
            }

            IReadOnlyCollection<long> personIds = query.Select(x => x.PersonId).Distinct().ToArray();
            return Task.FromResult(personIds);
        }

        public Task<IReadOnlyDictionary<long, TopUpStudentDisplaySummary>> FindDisplayByPersonIdsForTopUpAsync(
            IReadOnlyCollection<long> personIds,
            long organizationId,
            CancellationToken cancellationToken)
        {
            IReadOnlyDictionary<long, TopUpStudentDisplaySummary> result = students
                .Where(x => x.OrganizationId == organizationId && personIds.Contains(x.PersonId))
                .ToDictionary(
                    x => x.PersonId,
                    x => new TopUpStudentDisplaySummary(
                        x.PersonId,
                        $"STU-{x.PersonId}",
                        $"Student {x.PersonId}"));

            return Task.FromResult(result);
        }

        public Task<IReadOnlyList<AccountTaxonomyLevel>> GetAccountTaxonomyAsync(
            IReadOnlyCollection<long> scopedOrganizationIds,
            CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<AccountTaxonomyLevel>>(Array.Empty<AccountTaxonomyLevel>());
    }
}
