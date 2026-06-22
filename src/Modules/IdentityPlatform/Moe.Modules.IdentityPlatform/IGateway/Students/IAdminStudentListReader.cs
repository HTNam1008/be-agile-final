namespace Moe.Modules.IdentityPlatform.IGateway.Students;

internal interface IAdminStudentListReader
{
    Task<AdminStudentListPage> ListAsync(
        AdminStudentListCriteria criteria,
        IReadOnlyCollection<long> scopedOrganizationIds,
        bool hasGlobalAccess,
        DateOnly today,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<string>> ListClassesAsync(
        long organizationId,
        string levelCode,
        DateOnly today,
        CancellationToken cancellationToken);
}
