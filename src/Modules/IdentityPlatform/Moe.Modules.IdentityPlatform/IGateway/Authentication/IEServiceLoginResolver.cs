namespace Moe.Modules.IdentityPlatform.IGateway.Authentication;

public sealed record EServiceLoginResolution(
    long UserAccountId,
    long PersonId,
    string DisplayName);

public interface IEServiceLoginResolver
{
    Task<EServiceLoginResolution> ResolveAsync(
        SingpassLoginResult login,
        CancellationToken cancellationToken);
}
