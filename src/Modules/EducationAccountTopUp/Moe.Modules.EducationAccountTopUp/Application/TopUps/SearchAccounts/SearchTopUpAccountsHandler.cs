using Moe.Application.Abstractions.Clock;
using Moe.Application.Abstractions.Messaging;
using Moe.Application.Abstractions.Security;
using Moe.Modules.EducationAccountTopUp.Application.TopUps;
using Moe.Modules.EducationAccountTopUp.Application.TopUps.Filters;
using Moe.Modules.EducationAccountTopUp.IGateway.TopUps;
using Moe.Modules.IdentityPlatform.IGateway.Students;
using Moe.Modules.IdentityPlatform.IGateway.Students.TopUpSearch;
using Moe.SharedKernel.Results;

namespace Moe.Modules.EducationAccountTopUp.Application.TopUps.SearchAccounts;

internal sealed class SearchTopUpAccountsHandler(
    IAdminAccessControl adminAccess,
    IClock clock,
    ITopUpAccountProjectionRepository accounts,
    ITopUpStudentSearchDirectory students)
    : IQueryHandler<SearchTopUpAccountsQuery, SearchTopUpAccountsResponse>
{
    public async Task<Result<SearchTopUpAccountsResponse>> Handle(
        SearchTopUpAccountsQuery query,
        CancellationToken cancellationToken)
    {
        AdminOrganizationScope scope = adminAccess.ResolveOrganizationFilter(query.OrganizationId);
        if (!scope.HasAccess)
        {
            return Result<SearchTopUpAccountsResponse>.Failure(TopUpErrors.OrganizationOutsideScope);
        }
        long[] scopedOrganizationIds = scope.HasGlobalAccess ? [] : scope.ScopedOrganizationIds.ToArray();

        TopUpAccountFilter filter = new(
            query.Search,
            scope.OrganizationId,
            query.SchoolingStatusCode,
            query.LevelCode,
            query.ClassCode,
            query.AccountStatusCode,
            query.AgeFrom,
            query.AgeTo,
            query.BalanceFrom,
            query.BalanceTo);

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

        TopUpStudentSearchCriteria studentCriteria = filter.ToStudentCriteria(
            candidatePersonIds,
            accountSearchPersonIds,
            query.Page,
            query.PageSize);

        TopUpStudentSearchSummaryPage studentPage = await students.SearchForTopUpAsync(
            studentCriteria,
            scopedOrganizationIds,
            cancellationToken);

        long[] personIds = studentPage.Items.Select(x => x.PersonId).ToArray();
        IReadOnlyDictionary<long, TopUpAccountProjection> accountByPersonId = await accounts.FindByPersonIdsAsync(
            personIds,
            cancellationToken);

        TopUpAccountSearchItem[] items = studentPage.Items
            .Where(student => accountByPersonId.ContainsKey(student.PersonId))
            .Select(student => ToSearchItem(student, accountByPersonId[student.PersonId]))
            .ToArray();

        SearchTopUpAccountsResponse response = new(
            items,
            studentPage.Page,
            studentPage.PageSize,
            studentPage.TotalCount);

        return Result<SearchTopUpAccountsResponse>.Success(response);
    }

    private TopUpAccountSearchItem ToSearchItem(
        TopUpStudentSearchSummary student,
        TopUpAccountProjection account)
    {
        return new TopUpAccountSearchItem(
            account.EducationAccountId,
            student.PersonId,
            TopUpDisplayMasker.MaskAccountNumber(account.AccountNumber),
            student.StudentNumber,
            student.DisplayName,
            CalculateAge(student.DateOfBirth),
            account.AccountStatusCode,
            account.Balance,
            student.SchoolingStatusCode,
            student.LevelCode,
            student.ClassCode,
            student.OrganizationId);
    }

    private int CalculateAge(DateOnly dateOfBirth)
    {
        DateOnly today = DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);
        int age = today.Year - dateOfBirth.Year;

        if (dateOfBirth > today.AddYears(-age))
        {
            age--;
        }

        return age;
    }
}
