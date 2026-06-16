namespace Moe.Modules.IdentityPlatform.IGateway.Authentication;

public interface IExternalIdentityProvisioningRepository
{
    Task<bool> HasActiveExternalIdentityAsync(
        string identityProviderCode,
        string externalIssuer,
        string externalSubjectId,
        CancellationToken cancellationToken);
}
