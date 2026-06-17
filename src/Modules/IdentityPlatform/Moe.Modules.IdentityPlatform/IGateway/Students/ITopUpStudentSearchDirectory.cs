using Moe.Modules.IdentityPlatform.IGateway.Students.TopUpSearch;

namespace Moe.Modules.IdentityPlatform.IGateway.Students;

public interface ITopUpStudentSearchDirectory
{
    Task<TopUpStudentSearchSummaryPage> SearchForTopUpAsync(
        TopUpStudentSearchCriteria criteria,
        IReadOnlyCollection<long> scopedOrganizationIds,
        CancellationToken cancellationToken);
}
