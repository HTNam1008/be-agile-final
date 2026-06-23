namespace Moe.Modules.IdentityPlatform.IGateway.Admin;

public sealed record CreateEntraUserGatewayRequest(
    string UserPrincipalName,
    string DisplayName,
    string MailNickname,
    string TemporaryPassword,
    bool AccountEnabled);
