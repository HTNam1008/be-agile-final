using System.Text.Json;
using System.Text.Json.Serialization;

namespace Moe.Application.Abstractions.Audit;

public sealed record SchoolAuditContext(
    string ActionCode,
    string EntityTypeCode,
    long EntityId,
    long SchoolOrganizationId,
    SchoolAuditDetails? Details = null,
    DateTime? OccurredAtUtc = null);

public sealed record SchoolAuditDetails(
    string Summary,
    string? EntityDisplayName = null,
    IReadOnlyDictionary<string, long>? RelatedIds = null,
    SchoolAuditStatusTransition? StatusTransition = null,
    IReadOnlyCollection<string>? ChangedFields = null,
    string? ReasonCode = null,
    int? Count = null)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public string ToJson(long entityId)
        => JsonSerializer.Serialize(new
        {
            summary = Summary,
            entityId,
            entityDisplayName = EntityDisplayName,
            relatedIds = RelatedIds,
            statusTransition = StatusTransition,
            changedFields = ChangedFields,
            reasonCode = ReasonCode,
            count = Count
        }, JsonOptions);
}

public sealed record SchoolAuditStatusTransition(string? From, string To);
