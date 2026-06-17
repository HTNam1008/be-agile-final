using Moe.Modules.EducationAccountTopUp.IGateway.TopUps;
using Moe.Modules.IdentityPlatform.IGateway.Students.TopUpSearch;

namespace Moe.Modules.EducationAccountTopUp.Application.TopUps.Filters;

internal static class TopUpAccountFilterMapper
{
    public static TopUpAccountSearchCriteria ToAccountCriteria(this TopUpAccountFilter filter, bool includeSearch)
        => new(
            includeSearch ? filter.Search : null,
            filter.BalanceFrom,
            filter.BalanceTo,
            filter.AccountStatusCode);

    public static TopUpStudentSearchCriteria ToStudentCriteria(
        this TopUpAccountFilter filter,
        IReadOnlyCollection<long> candidatePersonIds,
        IReadOnlyCollection<long>? accountSearchPersonIds,
        int page,
        int pageSize)
        => new(
            filter.Search,
            candidatePersonIds,
            accountSearchPersonIds,
            filter.OrganizationId,
            filter.SchoolingStatusCode,
            filter.LevelCode,
            filter.ClassCode,
            filter.AgeFrom,
            filter.AgeTo,
            page,
            pageSize);
}
