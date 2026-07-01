using Moe.Modules.IdentityPlatform.IGateway.Students.TopUpSearch;

namespace Moe.Modules.IdentityPlatform.IGateway.Students;

public interface ITopUpStudentSearchDirectory
{
    Task<TopUpStudentSearchSummaryPage> SearchForTopUpAsync(
        TopUpStudentSearchCriteria criteria,
        IReadOnlyCollection<long> scopedOrganizationIds,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<long>> FindMatchingPersonIdsForTopUpAsync(
        TopUpStudentSearchCriteria criteria,
        IReadOnlyCollection<long> scopedOrganizationIds,
        CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<long, TopUpStudentDisplaySummary>> FindDisplayByPersonIdsForTopUpAsync(
        IReadOnlyCollection<long> personIds,
        long organizationId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<AccountTaxonomyLevel>> GetAccountTaxonomyAsync(
        IReadOnlyCollection<long> scopedOrganizationIds,
        CancellationToken cancellationToken);
}

public sealed record AccountTaxonomyLevel(string LevelCode, IReadOnlyList<string> Classes);
