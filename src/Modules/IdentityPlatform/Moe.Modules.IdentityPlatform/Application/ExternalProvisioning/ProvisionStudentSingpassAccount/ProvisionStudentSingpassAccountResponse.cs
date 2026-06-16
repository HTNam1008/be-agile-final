namespace Moe.Modules.IdentityPlatform.Application.ExternalProvisioning.ProvisionStudentSingpassAccount;

public sealed record ProvisionStudentSingpassAccountResponse(
    long IdentityProvisioningRequestId,
    long UserAccountId,
    string ProvisioningStatusCode,
    string AccountStatusCode);
