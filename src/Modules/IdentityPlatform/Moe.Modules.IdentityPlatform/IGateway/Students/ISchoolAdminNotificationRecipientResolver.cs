namespace Moe.Modules.IdentityPlatform.IGateway.Students;

public interface ISchoolAdminNotificationRecipientResolver
{
    Task<IReadOnlyCollection<long>> FindUserAccountIdsByOrganizationIdAsync(long organizationId, CancellationToken cancellationToken);
}
