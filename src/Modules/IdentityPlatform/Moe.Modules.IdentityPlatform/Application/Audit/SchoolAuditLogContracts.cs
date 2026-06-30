namespace Moe.Modules.IdentityPlatform.Application.Audit;

public sealed record SchoolAuditLogQuery(
    long? OrganizationId,
    string? ActionCode,
    string? EntityTypeCode,
    long? ActorId,
    DateTime? DateFromUtc,
    DateTime? DateToUtc,
    int Page,
    int PageSize);

public sealed record SchoolAuditLogPage(
    IReadOnlyCollection<SchoolAuditLogItem> Items,
    int Page,
    int PageSize,
    long Total,
    int TotalPages);

public sealed record SchoolAuditLogItem(
    long AuditLogId,
    long OrganizationId,
    DateTime OccurredAtUtc,
    long? ActorLoginAccountId,
    string ActorTypeCode,
    string ActionCode,
    string EntityTypeCode,
    long? EntityId,
    string OutcomeCode,
    string? Details);

public interface ISchoolAuditLogReader
{
    Task<SchoolAuditLogReadResult> ReadAsync(SchoolAuditLogQuery query, CancellationToken cancellationToken);
}

public sealed record SchoolAuditLogReadResult(
    SchoolAuditLogReadStatus Status,
    SchoolAuditLogPage? Page = null);

public enum SchoolAuditLogReadStatus
{
    Success,
    Forbidden,
    OrganizationRequired,
    OrganizationOutsideScope
}
