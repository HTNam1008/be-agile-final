using Microsoft.EntityFrameworkCore;
using Moe.Application.Abstractions.Security;
using Moe.Modules.IdentityPlatform.Application.Audit;
using Moe.Modules.IdentityPlatform.Domain.Audit;
using Moe.StudentFinance.Persistence;

namespace Moe.Modules.IdentityPlatform.Infrastructure.Audit;

internal sealed class SchoolAuditLogReader(
    MoeDbContext dbContext,
    IAdminAccessControl adminAccess) : ISchoolAuditLogReader
{
    public async Task<SchoolAuditLogReadResult> ReadAsync(
        SchoolAuditLogQuery query,
        CancellationToken cancellationToken)
    {
        if (adminAccess.IsHqAdmin || !adminAccess.IsSchoolAdmin)
        {
            return new SchoolAuditLogReadResult(SchoolAuditLogReadStatus.Forbidden);
        }

        long[] scopedOrganizationIds = adminAccess.ScopedOrganizationIds
            .Where(x => x > 0)
            .Distinct()
            .ToArray();

        long? organizationId = query.OrganizationId;
        if (organizationId is null)
        {
            if (scopedOrganizationIds.Length != 1)
            {
                return new SchoolAuditLogReadResult(SchoolAuditLogReadStatus.OrganizationRequired);
            }

            organizationId = scopedOrganizationIds[0];
        }

        if (!scopedOrganizationIds.Contains(organizationId.Value))
        {
            return new SchoolAuditLogReadResult(SchoolAuditLogReadStatus.OrganizationOutsideScope);
        }

        int page = Math.Max(1, query.Page);
        int pageSize = Math.Clamp(query.PageSize, 1, 100);

        IQueryable<AuditLog> logs = dbContext.Set<AuditLog>()
            .AsNoTracking()
            .Where(x => x.OrganizationId == organizationId.Value);

        if (!string.IsNullOrWhiteSpace(query.ActionCode))
        {
            string actionCode = query.ActionCode.Trim().ToUpperInvariant();
            logs = logs.Where(x => x.ActionCode == actionCode);
        }

        if (!string.IsNullOrWhiteSpace(query.EntityTypeCode))
        {
            string entityTypeCode = query.EntityTypeCode.Trim();
            logs = logs.Where(x => x.EntityTypeCode == entityTypeCode);
        }

        if (query.ActorId.HasValue)
        {
            logs = logs.Where(x => x.ActorLoginAccountId == query.ActorId.Value);
        }

        if (query.DateFromUtc.HasValue)
        {
            logs = logs.Where(x => x.OccurredAtUtc >= query.DateFromUtc.Value);
        }

        if (query.DateToUtc.HasValue)
        {
            logs = logs.Where(x => x.OccurredAtUtc < query.DateToUtc.Value);
        }

        long total = await logs.LongCountAsync(cancellationToken);
        SchoolAuditLogItem[] items = await logs
            .OrderByDescending(x => x.OccurredAtUtc)
            .ThenByDescending(x => x.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new SchoolAuditLogItem(
                x.Id,
                x.OrganizationId!.Value,
                x.OccurredAtUtc,
                x.ActorLoginAccountId,
                x.ActorTypeCode,
                x.ActionCode,
                x.EntityTypeCode,
                x.EntityId,
                x.OutcomeCode,
                x.ChangedFieldsJson))
            .ToArrayAsync(cancellationToken);

        var resultPage = new SchoolAuditLogPage(
            items,
            page,
            pageSize,
            total,
            (int)Math.Ceiling(total / (double)pageSize));

        return new SchoolAuditLogReadResult(SchoolAuditLogReadStatus.Success, resultPage);
    }
}

