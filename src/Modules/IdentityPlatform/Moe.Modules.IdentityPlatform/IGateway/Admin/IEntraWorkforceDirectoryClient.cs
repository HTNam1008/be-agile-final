namespace Moe.Modules.IdentityPlatform.IGateway.Admin;

public interface IEntraWorkforceDirectoryClient
{
    Task<CreateEntraUserGatewayResult> CreateUserAsync(
        CreateEntraUserGatewayRequest request,
        CancellationToken cancellationToken);

    Task DeleteUserAsync(string externalObjectId, CancellationToken cancellationToken);
}
