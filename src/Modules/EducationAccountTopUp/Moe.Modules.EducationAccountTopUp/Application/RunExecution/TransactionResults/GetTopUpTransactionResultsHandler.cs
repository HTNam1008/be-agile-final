using Moe.Application.Abstractions.Messaging;
using Moe.Infrastructure.Shared.Api;
using Moe.Modules.EducationAccountTopUp.Application.History;
using Moe.Modules.EducationAccountTopUp.Application.TopUps;
using Moe.Modules.EducationAccountTopUp.Domain.EducationAccounts;
using Moe.Modules.EducationAccountTopUp.Domain.TopUps;
using Moe.Modules.EducationAccountTopUp.IGateway.RunSummary;
using Moe.Modules.EducationAccountTopUp.IGateway.TopUps;
using Moe.Modules.EducationAccountTopUp.IGateway.TransactionResults;
using Moe.Modules.IdentityPlatform.IGateway.Students;
using Moe.Modules.IdentityPlatform.IGateway.Students.TopUpSearch;
using Moe.SharedKernel.Results;

namespace Moe.Modules.EducationAccountTopUp.Application.RunExecution.TransactionResults;

internal sealed class GetTopUpTransactionResultsHandler(
    ITopUpRunSummaryReader runReader,
    ITopUpAccessScopeResolver accessScopeResolver,
    ITopUpTransactionResultsReader transactionReader,
    ITopUpAccountProjectionRepository accounts,
    ITopUpStudentSearchDirectory students)
    : IQueryHandler<GetTopUpTransactionResultsQuery, PageResponse<TopUpTransactionResultItem>>
{
    public async Task<Result<PageResponse<TopUpTransactionResultItem>>> Handle(
        GetTopUpTransactionResultsQuery query,
        CancellationToken cancellationToken)
    {
        RunSummaryProjection? run = await runReader.GetByIdAsync(
            query.RunId,
            cancellationToken);

        if (run is null)
        {
            return Result<PageResponse<TopUpTransactionResultItem>>.Failure(
                Moe.Modules.EducationAccountTopUp.Domain.TopUps.TopUpErrors.RunNotFound);
        }

        Result<TopUpAccessScope> accessResult =
            accessScopeResolver.Resolve(run.OrganizationId);

        if (accessResult.IsFailure)
        {
            return Result<PageResponse<TopUpTransactionResultItem>>.Failure(
                accessResult.Error);
        }

        IReadOnlyCollection<long>? matchingAccountIds =
            await ResolveMatchingAccountIdsAsync(
                query.Filter.StudentOrAccountSearch,
                run.OrganizationId,
                cancellationToken);

        TransactionResultsPage page = await transactionReader.GetPageAsync(
            query.RunId,
            query.Filter,
            matchingAccountIds,
            query.Page,
            query.PageSize,
            cancellationToken);

        if (page.Items.Count == 0)
        {
            return Result<PageResponse<TopUpTransactionResultItem>>.Success(
                new PageResponse<TopUpTransactionResultItem>(
                    [],
                    query.Page,
                    query.PageSize,
                    page.TotalCount));
        }

        long[] educationAccountIds = page.Items
            .Select(x => x.EducationAccountId)
            .Distinct()
            .ToArray();

        IReadOnlyDictionary<long, TopUpAccountProjection> accountById =
            await accounts.FindByEducationAccountIdsAsync(
                educationAccountIds,
                cancellationToken);

        long[] personIds = accountById.Values
            .Select(x => x.PersonId)
            .Distinct()
            .ToArray();

        IReadOnlyDictionary<long, TopUpStudentDisplaySummary> studentByPersonId =
            await students.FindDisplayByPersonIdsForTopUpAsync(
                personIds,
                run.OrganizationId,
                cancellationToken);

        TopUpTransactionResultItem[] items = page.Items
            .Select(item => MapItem(item, accountById, studentByPersonId))
            .ToArray();

        return Result<PageResponse<TopUpTransactionResultItem>>.Success(
            new PageResponse<TopUpTransactionResultItem>(
                items,
                query.Page,
                query.PageSize,
                page.TotalCount));
    }

    private async Task<IReadOnlyCollection<long>?> ResolveMatchingAccountIdsAsync(
        string? search,
        long organizationId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return null;
        }

        string normalizedSearch = search.Trim();
        IReadOnlyCollection<long> accountSearchPersonIds =
            await accounts.FindMatchingPersonIdsAsync(
                new TopUpAccountSearchCriteria(
                    normalizedSearch,
                    BalanceFrom: null,
                    BalanceTo: null,
                    AccountStatusCode: null),
                cancellationToken);

        TopUpStudentSearchCriteria studentCriteria = new(
            normalizedSearch,
            CandidatePersonIds: null,
            AccountSearchPersonIds: accountSearchPersonIds,
            OrganizationId: organizationId,
            SchoolingStatusCode: null,
            LevelCode: null,
            ClassCode: null,
            AgeFrom: null,
            AgeTo: null,
            Page: 1,
            PageSize: 1);

        IReadOnlyCollection<long> matchingPersonIds =
            await students.FindMatchingPersonIdsForTopUpAsync(
                studentCriteria,
                [organizationId],
                cancellationToken);

        IReadOnlyDictionary<long, TopUpAccountProjection> matchingAccounts =
            await accounts.FindByPersonIdsAsync(
                matchingPersonIds,
                cancellationToken);

        return matchingAccounts.Values
            .Select(x => x.EducationAccountId)
            .Distinct()
            .ToArray();
    }

    private static TopUpTransactionResultItem MapItem(
        TopUpTransactionResultProjection item,
        IReadOnlyDictionary<long, TopUpAccountProjection> accountById,
        IReadOnlyDictionary<long, TopUpStudentDisplaySummary> studentByPersonId)
    {
        accountById.TryGetValue(
            item.EducationAccountId,
            out TopUpAccountProjection? account);

        TopUpStudentDisplaySummary? student = null;
        if (account is not null)
        {
            studentByPersonId.TryGetValue(account.PersonId, out student);
        }

        return new TopUpTransactionResultItem(
            item.TransactionId,
            account is null
                ? "****"
                : TopUpDisplayMasker.MaskAccountNumber(account.AccountNumber),
            student is null
                ? null
                : TopUpDisplayMasker.MaskStudentNumber(student.StudentNumber),
            student?.DisplayName ?? "Unavailable",
            item.Amount,
            CurrencyCodes.SingaporeDollar,
            item.Status,
            TopUpSafeReasonPresenter.Present(item.Reason),
            item.AccountTransactionId,
            item.CreatedAtUtc,
            item.CompletedAtUtc);
    }
}
