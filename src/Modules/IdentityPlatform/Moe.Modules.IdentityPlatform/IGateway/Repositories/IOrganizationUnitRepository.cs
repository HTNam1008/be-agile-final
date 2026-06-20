using Moe.Modules.IdentityPlatform.Application.Organizations;

namespace Moe.Modules.IdentityPlatform.IGateway.Repositories;

public interface IOrganizationUnitRepository
{
    Task<IReadOnlyCollection<OrganizationUnitSummary>> ListActiveAsync(
        IReadOnlyCollection<long>? organizationIds,
        CancellationToken cancellationToken);

    Task<OrganizationUnitSummary?> FindActiveSchoolByNameAsync(
        string schoolName,
        CancellationToken cancellationToken);

    Task<OrganizationUnitSummary?> FindActiveSchoolByIdAsync(
        long organizationId,
        CancellationToken cancellationToken);
}
