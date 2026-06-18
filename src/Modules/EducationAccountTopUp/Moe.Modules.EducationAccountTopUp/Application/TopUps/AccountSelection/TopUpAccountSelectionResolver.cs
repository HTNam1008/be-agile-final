using FluentValidation;
using Moe.Application.Abstractions.Security;
using Moe.Modules.EducationAccountTopUp.Application.TopUps.Filters;
using Moe.Modules.EducationAccountTopUp.Contracts.TopUps.Enums;
using Moe.Modules.EducationAccountTopUp.IGateway.TopUps;
using Moe.Modules.IdentityPlatform.IGateway.Students;
using Moe.Modules.IdentityPlatform.IGateway.Students.TopUpSearch;
using Moe.SharedKernel.Results;

namespace Moe.Modules.EducationAccountTopUp.Application.TopUps.AccountSelection;

internal sealed class TopUpAccountSelectionResolver(
    ICurrentUser currentUser,
    IValidator<TopUpAccountSelection> validator,
    ITopUpAccountProjectionRepository accounts,
    ITopUpStudentSearchDirectory students) : ITopUpAccountSelectionResolver
{
    public async Task<Result<TopUpAccountSelectionResolution>> ResolveAsync(
        TopUpAccountSelection selection,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(selection, cancellationToken);

        if (!validation.IsValid)
        {
            return Result<TopUpAccountSelectionResolution>.Failure(TopUpErrors.InvalidAccountSelection);
        }

        long[] scopedOrganizationIds = currentUser.OrganizationUnitIds.ToArray();

        if (scopedOrganizationIds.Length == 0)
        {
            return Result<TopUpAccountSelectionResolution>.Failure(TopUpErrors.AdminOrganizationScopeRequired);
        }

        return selection.Mode switch
        {
            TopUpAccountSelectionMode.ExplicitIds => await ResolveExplicitAsync(
                selection.SelectedEducationAccountIds,
                scopedOrganizationIds,
                cancellationToken),
            TopUpAccountSelectionMode.AllMatchingFilter => await ResolveAllMatchingAsync(
                selection.Filter!,
                selection.ExcludedEducationAccountIds,
                scopedOrganizationIds,
                cancellationToken),
            _ => Result<TopUpAccountSelectionResolution>.Failure(TopUpErrors.InvalidAccountSelection)
        };
    }

    private async Task<Result<TopUpAccountSelectionResolution>> ResolveExplicitAsync(
        IReadOnlyCollection<long> selectedEducationAccountIds,
        IReadOnlyCollection<long> scopedOrganizationIds,
        CancellationToken cancellationToken)
    {
        long[] selectedIds = selectedEducationAccountIds.Distinct().ToArray();
        IReadOnlyDictionary<long, TopUpAccountProjection> selectedAccounts = await accounts.FindByEducationAccountIdsAsync(
            selectedIds,
            cancellationToken);

        if (selectedAccounts.Count != selectedIds.Length)
        {
            return Result<TopUpAccountSelectionResolution>.Failure(TopUpErrors.InvalidAccountSelection);
        }

        long[] selectedPersonIds = selectedAccounts.Values.Select(x => x.PersonId).Distinct().ToArray();
        IReadOnlyCollection<long> scopedPersonIds = await students.FindMatchingPersonIdsForTopUpAsync(
            CreateScopeOnlyCriteria(selectedPersonIds),
            scopedOrganizationIds,
            cancellationToken);

        if (scopedPersonIds.Count != selectedPersonIds.Length)
        {
            return Result<TopUpAccountSelectionResolution>.Failure(TopUpErrors.AccountSelectionOutsideScope);
        }

        return Result<TopUpAccountSelectionResolution>.Success(
            new TopUpAccountSelectionResolution(
                selectedIds,
                selectedIds.Length,
                0,
                selectedIds.Length));
    }

    private async Task<Result<TopUpAccountSelectionResolution>> ResolveAllMatchingAsync(
        TopUpAccountFilter filter,
        IReadOnlyCollection<long> excludedEducationAccountIds,
        IReadOnlyCollection<long> scopedOrganizationIds,
        CancellationToken cancellationToken)
    {
        if (filter.OrganizationId.HasValue && !scopedOrganizationIds.Contains(filter.OrganizationId.Value))
        {
            return Result<TopUpAccountSelectionResolution>.Failure(TopUpErrors.OrganizationOutsideScope);
        }

        IReadOnlyCollection<long> matchedPersonIds = await ResolveMatchingPersonIdsAsync(
            filter,
            scopedOrganizationIds,
            cancellationToken);

        IReadOnlyDictionary<long, TopUpAccountProjection> matchedAccountsByPersonId = await accounts.FindByPersonIdsAsync(
            matchedPersonIds,
            cancellationToken);

        long[] matchedEducationAccountIds = matchedAccountsByPersonId.Values
            .Select(x => x.EducationAccountId)
            .Distinct()
            .ToArray();

        HashSet<long> matchedEducationAccountIdSet = matchedEducationAccountIds.ToHashSet();
        long[] excludedIds = excludedEducationAccountIds.Distinct().ToArray();

        if (excludedIds.Any(id => !matchedEducationAccountIdSet.Contains(id)))
        {
            return Result<TopUpAccountSelectionResolution>.Failure(TopUpErrors.AccountSelectionOutsideScope);
        }

        HashSet<long> excludedIdSet = excludedIds.ToHashSet();
        long[] selectedIds = matchedEducationAccountIds
            .Where(id => !excludedIdSet.Contains(id))
            .ToArray();

        return Result<TopUpAccountSelectionResolution>.Success(
            new TopUpAccountSelectionResolution(
                selectedIds,
                matchedEducationAccountIds.Length,
                excludedIds.Length,
                selectedIds.Length));
    }

    private async Task<IReadOnlyCollection<long>> ResolveMatchingPersonIdsAsync(
        TopUpAccountFilter filter,
        IReadOnlyCollection<long> scopedOrganizationIds,
        CancellationToken cancellationToken)
    {
        IReadOnlyCollection<long> candidatePersonIds = await accounts.FindMatchingPersonIdsAsync(
            filter.ToAccountCriteria(includeSearch: false),
            cancellationToken);

        IReadOnlyCollection<long>? accountSearchPersonIds = null;

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            accountSearchPersonIds = await accounts.FindMatchingPersonIdsAsync(
                filter.ToAccountCriteria(includeSearch: true),
                cancellationToken);
        }

        TopUpStudentSearchCriteria criteria = filter.ToStudentCriteria(
            candidatePersonIds,
            accountSearchPersonIds,
            page: 1,
            pageSize: 1);

        return await students.FindMatchingPersonIdsForTopUpAsync(
            criteria,
            scopedOrganizationIds,
            cancellationToken);
    }

    private static TopUpStudentSearchCriteria CreateScopeOnlyCriteria(IReadOnlyCollection<long> candidatePersonIds)
        => new(
            null,
            candidatePersonIds,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            1,
            1);
}
