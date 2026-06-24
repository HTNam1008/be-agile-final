namespace Moe.Modules.IdentityPlatform.IGateway.Accounts;

public interface ILoginAccountDisplayDirectory
{
    Task<IReadOnlyDictionary<long, string>> FindDisplayNamesAsync(
        IReadOnlyCollection<long> loginAccountIds,
        CancellationToken cancellationToken);
}
