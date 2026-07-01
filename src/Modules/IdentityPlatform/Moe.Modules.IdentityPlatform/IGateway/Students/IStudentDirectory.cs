namespace Moe.Modules.IdentityPlatform.IGateway.Students;

public interface IStudentDirectory
{
    Task<StudentSummary?> FindByPersonIdAsync(long personId, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<long>> FindActivePersonIdsByOrganizationAsync(
        long organizationId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<AdminStudentSearchSummary>> ListByOrganizationAsync(
        AdminStudentSearchCriteria criteria,
        CancellationToken cancellationToken);

    Task<long> CountByOrganizationAsync(
        AdminStudentSearchCriteria criteria,
        CancellationToken cancellationToken);
}
