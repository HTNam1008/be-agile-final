namespace Moe.Modules.IdentityPlatform.Application.ExternalProvisioning.DisableUserAccount;

public sealed record DisableUserAccountResponse(long UserAccountId, string AccountStatusCode);
