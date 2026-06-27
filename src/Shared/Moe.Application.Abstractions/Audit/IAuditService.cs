namespace Moe.Application.Abstractions.Audit;

public interface IAuditService
{
    Task RecordAsync(
        string actionCode,
        string entityTypeCode,
        string entityId,
        string? detailsJson = null,
        CancellationToken cancellationToken = default);

    Task RecordSchoolActionAsync(
        SchoolAuditContext context,
        CancellationToken cancellationToken = default);
}
