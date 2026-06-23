using Moe.Application.Abstractions.Security;
using Moe.SharedKernel.Results;

namespace Moe.Infrastructure.Shared.Security;

internal sealed class AdminAccessControl(ICurrentUser currentUser) : IAdminAccessControl
{
    private static readonly Error OrganizationOutsideScope = new(
        "AUTH.ORGANIZATION_OUTSIDE_SCOPE",
        "The requested organization is outside the current admin's scope.");

    public bool IsHqAdmin => currentUser.Roles.Contains("HQ_ADMIN", StringComparer.OrdinalIgnoreCase);

    public bool IsSchoolAdmin => currentUser.Roles.Contains("SCHOOL_ADMIN", StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<long> ScopedOrganizationIds
    {
        get
        {
            IEnumerable<long> organizationIds = currentUser.OrganizationUnitId is long primaryOrganizationId
                ? currentUser.OrganizationUnitIds.Append(primaryOrganizationId)
                : currentUser.OrganizationUnitIds;

            return organizationIds
                .Where(id => id > 0)
                .Distinct()
                .ToArray();
        }
    }

    public bool CanAccessOrganization(long organizationId)
    {
        return organizationId > 0
            && (IsHqAdmin || ScopedOrganizationIds.Contains(organizationId));
    }

    public Result EnsureCanAccessOrganization(long organizationId)
    {
        return CanAccessOrganization(organizationId)
            ? Result.Success()
            : Result.Failure(OrganizationOutsideScope);
    }

    public AdminOrganizationScope ResolveOrganizationFilter(long? requestedOrganizationId)
    {
        if (IsHqAdmin)
        {
            return requestedOrganizationId is long requested
                ? new AdminOrganizationScope(requested > 0, true, requested, [])
                : new AdminOrganizationScope(true, true, null, []);
        }

        long[] scopedOrganizationIds = ScopedOrganizationIds.ToArray();
        if (requestedOrganizationId is long requestedOrganization)
        {
            return new AdminOrganizationScope(
                scopedOrganizationIds.Contains(requestedOrganization),
                false,
                requestedOrganization,
                scopedOrganizationIds);
        }

        return scopedOrganizationIds.Length switch
        {
            0 => new AdminOrganizationScope(false, false, null, scopedOrganizationIds),
            1 => new AdminOrganizationScope(true, false, scopedOrganizationIds[0], scopedOrganizationIds),
            _ => new AdminOrganizationScope(true, false, null, scopedOrganizationIds)
        };
    }
}
