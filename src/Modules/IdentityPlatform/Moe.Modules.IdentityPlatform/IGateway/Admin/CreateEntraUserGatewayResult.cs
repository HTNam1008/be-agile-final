namespace Moe.Modules.IdentityPlatform.IGateway.Admin;

public sealed record CreateEntraUserGatewayResult(
    string ExternalObjectId,
    string UserPrincipalName,
    string DisplayName,
    string? Mail);
