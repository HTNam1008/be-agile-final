using Moe.SharedKernel.Domain;

namespace Moe.Modules.IdentityPlatform.Domain.Audit;

internal sealed class AuditLog : Entity<long>
{
    private AuditLog() : base(0) { }

    public static AuditLog Record(
        string auditScopeCode,
        long? organizationId,
        string actorTypeCode,
        long? actorLoginAccountId,
        long? personId,
        string actionCode,
        string entityTypeCode,
        long entityId,
        string? changedFieldsJson,
        DateTime occurredAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(auditScopeCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(actorTypeCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(actionCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(entityTypeCode);

        return new AuditLog
        {
            AuditScopeCode = auditScopeCode,
            OrganizationId = organizationId,
            ActorTypeCode = actorTypeCode,
            ActorLoginAccountId = actorLoginAccountId,
            PersonId = personId,
            ActionCode = actionCode,
            EntityTypeCode = entityTypeCode,
            EntityId = entityId,
            OutcomeCode = AuditOutcomeCodes.Success,
            ChangedFieldsJson = changedFieldsJson,
            OccurredAtUtc = occurredAtUtc
        };
    }

    public string AuditScopeCode { get; private set; } = string.Empty;
    public long? OrganizationId { get; private set; }
    public string ActorTypeCode { get; private set; } = string.Empty;
    public long? ActorLoginAccountId { get; private set; }
    public string? ActorNameSnapshot { get; private set; }
    public long? PersonId { get; private set; }
    public string ActionCode { get; private set; } = string.Empty;
    public string EntityTypeCode { get; private set; } = string.Empty;
    public long? EntityId { get; private set; }
    public string OutcomeCode { get; private set; } = string.Empty;
    public string? Reason { get; private set; }
    public string? ChangedFieldsJson { get; private set; }
    public string? CorrelationId { get; private set; }
    public DateTime OccurredAtUtc { get; private set; }
    public string? IpAddress { get; private set; }
}

internal static class AuditOutcomeCodes
{
    public const string Success = "SUCCESS";
}
