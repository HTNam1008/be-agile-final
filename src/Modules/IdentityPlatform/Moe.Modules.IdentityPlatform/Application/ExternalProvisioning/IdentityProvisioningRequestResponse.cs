namespace Moe.Modules.IdentityPlatform.Application.ExternalProvisioning;

public sealed record IdentityProvisioningRequestResponse(
    long IdentityProvisioningRequestId,
    long PersonId,
    string IdentityProviderCode,
    string ProvisioningStatusCode,
    string? ExternalSubjectId,
    string CorrelationId,
    string? FailureCode,
    string? FailureReason);
