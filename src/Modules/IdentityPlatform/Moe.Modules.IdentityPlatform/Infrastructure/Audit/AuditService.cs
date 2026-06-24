using Moe.Application.Abstractions.Audit;
using Moe.Application.Abstractions.Clock;
using Moe.Application.Abstractions.Security;
using Moe.Modules.IdentityPlatform.Domain.Audit;
using Moe.StudentFinance.Persistence;

namespace Moe.Modules.IdentityPlatform.Infrastructure.Audit;

internal sealed class AuditService(
    MoeDbContext dbContext,
    ICurrentUser currentUser,
    IClock clock) : IAuditService
{
    public Task RecordAsync(
        string actionCode,
        string entityTypeCode,
        string entityId,
        string? detailsJson = null,
        CancellationToken cancellationToken = default)
    {
        long parsedEntityId = long.Parse(entityId);
        string actorTypeCode = ResolveActorTypeCode(currentUser.Portal);

        AuditLog auditLog = AuditLog.Record(
            auditScopeCode: currentUser.Portal,
            organizationId: currentUser.OrganizationUnitId,
            actorTypeCode: actorTypeCode,
            actorLoginAccountId: currentUser.UserAccountId,
            personId: currentUser.PersonId,
            actionCode: actionCode,
            entityTypeCode: entityTypeCode,
            entityId: parsedEntityId,
            changedFieldsJson: detailsJson,
            occurredAtUtc: clock.UtcNow.UtcDateTime);

        dbContext.Set<AuditLog>().Add(auditLog);
        return Task.CompletedTask;
    }

    private static string ResolveActorTypeCode(string portal)
    {
        return string.Equals(portal, "AdminPortal", StringComparison.OrdinalIgnoreCase)
            ? "ADMIN"
            : "USER";
    }
}
