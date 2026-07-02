namespace Moe.Modules.IdentityPlatform.IGateway.Students;

public interface IStudentNotificationRecipientResolver
{
    Task<long?> FindUserAccountIdByPersonIdAsync(long personId, CancellationToken cancellationToken);
}
