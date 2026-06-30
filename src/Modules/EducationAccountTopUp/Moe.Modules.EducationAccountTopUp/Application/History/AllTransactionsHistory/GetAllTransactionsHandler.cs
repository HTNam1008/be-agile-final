using Moe.Application.Abstractions.Messaging;
using Moe.Infrastructure.Shared.Api;
using Moe.Modules.EducationAccountTopUp.Application.RunExecution.TransactionResults;
using Moe.Modules.EducationAccountTopUp.Application.TopUps;
using Moe.Modules.EducationAccountTopUp.Domain.EducationAccounts;
using Moe.Modules.EducationAccountTopUp.IGateway.History;
using Moe.Modules.EducationAccountTopUp.IGateway.TopUps;
using Moe.Modules.IdentityPlatform.IGateway.Students;
using Moe.Modules.IdentityPlatform.IGateway.Students.TopUpSearch;
using Moe.SharedKernel.Results;

namespace Moe.Modules.EducationAccountTopUp.Application.History.AllTransactionsHistory;

internal sealed class GetAllTransactionsHandler(
    ITopUpAccessScopeResolver accessScopeResolver,
    ITopUpTransactionReader transactionReader,
    ITopUpAccountProjectionRepository accounts,
    ITopUpStudentSearchDirectory students)
    : IQueryHandler<GetAllTransactionsQuery, PageResponse<AllTransactionsItem>>
{
    public async Task<Result<PageResponse<AllTransactionsItem>>> Handle(
        GetAllTransactionsQuery query,
        CancellationToken cancellationToken)
    {
        Result<TopUpAccessScope> accessResult =
            accessScopeResolver.Resolve(query.Filter.OrganizationId);

        if (accessResult.IsFailure)
        {
            return Result<PageResponse<AllTransactionsItem>>.Failure(accessResult.Error);
        }

        TransactionHistoryPage page = await transactionReader.GetAllTransactionsAsync(
            query.Filter,
            query.Page,
            query.PageSize,
            cancellationToken);

        if (page.Items.Count == 0)
        {
            return Result<PageResponse<AllTransactionsItem>>.Success(
                new PageResponse<AllTransactionsItem>(
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
                organizationId: 0,
                cancellationToken);

        AllTransactionsItem[] items = page.Items
            .Select(item => MapItem(item, accountById, studentByPersonId))
            .ToArray();

        return Result<PageResponse<AllTransactionsItem>>.Success(
            new PageResponse<AllTransactionsItem>(
                items,
                query.Page,
                query.PageSize,
                page.TotalCount));
    }

    private static AllTransactionsItem MapItem(
        TransactionHistoryProjection item,
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

        return new AllTransactionsItem(
            item.TransactionId,
            item.RunId,
            item.EducationAccountId,
            item.CampaignCode,
            item.CampaignName,
            account is null
                ? "****"
                : TopUpDisplayMasker.MaskAccountNumber(account.AccountNumber),
            student is null
                ? null
                : TopUpDisplayMasker.MaskStudentNumber(student.StudentNumber),
            student?.DisplayName ?? "Unavailable",
            item.Amount,
            CurrencyCodes.SingaporeDollar,
            item.StatusCode,
            TopUpSafeReasonPresenter.Present(item.Reason),
            item.CompletedAtUtc,
            item.RunDateUtc);
    }
}
