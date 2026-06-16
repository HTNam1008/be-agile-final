namespace Moe.Modules.IdentityPlatform.IGateway.People;

public interface IPersonDirectory
{
    Task<PersonSummary?> FindAsync(long personId, CancellationToken cancellationToken);
}
