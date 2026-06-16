namespace Moe.Modules.IdentityPlatform.IGateway.Authentication;

public interface ILocalIdentityDirectory
{
    Task<LocalIdentitySummary?> GetCurrentAsync(CancellationToken cancellationToken);
}
