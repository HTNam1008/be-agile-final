using Moe.SharedKernel.Domain;

namespace Moe.Modules.IdentityPlatform.Domain.Iam;

internal sealed class IdentityProvisioningRequest : AggregateRoot<long>
{
    private IdentityProvisioningRequest() : base(0) { }

    private IdentityProvisioningRequest(
        long personId,
        string identityProviderCode,
        string externalIssuer,
        string displayNameSnapshot,
        string idempotencyKey,
        long requestedByUserAccountId,
        DateTime requestedAtUtc,
        string correlationId) : base(0)
    {
        PersonId = personId;
        IdentityProviderCode = identityProviderCode;
        ExternalIssuer = externalIssuer;
        DisplayNameSnapshot = displayNameSnapshot;
        IdempotencyKey = idempotencyKey;
        RequestedByUserAccountId = requestedByUserAccountId;
        RequestedAtUtc = requestedAtUtc;
        CorrelationId = correlationId;
        ProvisioningStatusCode = ProvisioningStatusCodes.Pending;
    }

    public long PersonId { get; private set; }
    public string IdentityProviderCode { get; private set; } = string.Empty;
    public string ExternalIssuer { get; private set; } = string.Empty;
    public string? RequestedEmailNormalized { get; private set; }
    public string DisplayNameSnapshot { get; private set; } = string.Empty;
    public string ProvisioningStatusCode { get; private set; } = string.Empty;
    public string IdempotencyKey { get; private set; } = string.Empty;
    public string? ExternalTenantId { get; private set; }
    public string? ExternalObjectId { get; private set; }
    public string? ExternalSubjectId { get; private set; }
    public long RequestedByUserAccountId { get; private set; }
    public DateTime RequestedAtUtc { get; private set; }
    public DateTime? CompletedAtUtc { get; private set; }
    public string? FailureCode { get; private set; }
    public string? FailureReason { get; private set; }
    public int RetryCount { get; private set; }
    public string CorrelationId { get; private set; } = string.Empty;
    public byte[] RowVersion { get; private set; } = [];

    public static IdentityProvisioningRequest CreateSingpassStudent(
        long personId,
        string externalIssuer,
        string displayNameSnapshot,
        string idempotencyKey,
        long requestedByUserAccountId,
        DateTime requestedAtUtc,
        string correlationId)
    {
        return new IdentityProvisioningRequest(
            personId,
            IdentityProviderCodes.Singpass,
            externalIssuer,
            displayNameSnapshot,
            idempotencyKey,
            requestedByUserAccountId,
            requestedAtUtc,
            correlationId);
    }

    public void Complete(string externalSubjectId, DateTime completedAtUtc)
    {
        ExternalTenantId = null;
        ExternalObjectId = null;
        ExternalSubjectId = externalSubjectId;
        ProvisioningStatusCode = ProvisioningStatusCodes.Completed;
        CompletedAtUtc = completedAtUtc;
        FailureCode = null;
        FailureReason = null;
    }

    public void FailManualReview(string failureCode, string failureReason)
    {
        ProvisioningStatusCode = ProvisioningStatusCodes.FailedManualReview;
        FailureCode = failureCode;
        FailureReason = failureReason;
    }
}
