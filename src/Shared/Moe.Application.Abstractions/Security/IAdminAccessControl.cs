using Moe.SharedKernel.Results;

namespace Moe.Application.Abstractions.Security;

public interface IAdminAccessControl
{
    bool IsHqAdmin { get; }
    bool IsSchoolAdmin { get; }
    IReadOnlyCollection<long> ScopedOrganizationIds { get; }
    bool CanAccessOrganization(long organizationId);
    Result EnsureCanAccessOrganization(long organizationId);
    AdminOrganizationScope ResolveOrganizationFilter(long? requestedOrganizationId);
}

public sealed record AdminOrganizationScope(
    bool HasAccess,
    bool HasGlobalAccess,
    long? OrganizationId,
    IReadOnlyCollection<long> ScopedOrganizationIds);
