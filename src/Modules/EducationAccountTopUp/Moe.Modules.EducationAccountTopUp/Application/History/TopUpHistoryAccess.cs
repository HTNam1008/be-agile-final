using Moe.Application.Abstractions.Security;
using Moe.Modules.EducationAccountTopUp.Contracts.TopUps;
using Moe.SharedKernel.Results;

namespace Moe.Modules.EducationAccountTopUp.Application.History;

internal sealed record TopUpAccessScope(
    bool HasGlobalAccess,
    IReadOnlyCollection<long> OrganizationIds)
{
    public bool RequiresOrganizationFilter => !HasGlobalAccess || OrganizationIds.Count > 0;
}

internal interface ITopUpAccessScopeResolver
{
    Result<TopUpAccessScope> Resolve(long? requestedOrganizationId);
}

internal sealed class TopUpAccessScopeResolver(ICurrentUser currentUser) : ITopUpAccessScopeResolver
{
    public Result<TopUpAccessScope> Resolve(long? requestedOrganizationId)
    {
        bool hasGlobalAccess = currentUser.HasPermission(TopUpPermissions.ViewAll);
        bool hasScopedAccess = currentUser.HasPermission(TopUpPermissions.Manage);

        if (!hasGlobalAccess && !hasScopedAccess)
        {
            return Result<TopUpAccessScope>.Failure(TopUpHistoryErrors.AccessDenied);
        }

        IEnumerable<long> currentOrganizationIds = currentUser.OrganizationUnitId is long currentOrganizationId
            ? currentUser.OrganizationUnitIds.Append(currentOrganizationId)
            : currentUser.OrganizationUnitIds;

        long[] organizationIds = currentOrganizationIds
            .Distinct()
            .ToArray();

        if (requestedOrganizationId.HasValue)
        {
            if (!hasGlobalAccess && !organizationIds.Contains(requestedOrganizationId.Value))
            {
                return Result<TopUpAccessScope>.Failure(TopUpHistoryErrors.OrganizationOutsideScope);
            }

            return Result<TopUpAccessScope>.Success(
                new TopUpAccessScope(hasGlobalAccess, [requestedOrganizationId.Value]));
        }

        if (!hasGlobalAccess && organizationIds.Length == 0)
        {
            return Result<TopUpAccessScope>.Failure(TopUpHistoryErrors.OrganizationScopeRequired);
        }

        return Result<TopUpAccessScope>.Success(
            new TopUpAccessScope(hasGlobalAccess, organizationIds));
    }
}

internal static class TopUpHistoryErrors
{
    public static readonly Error AccessDenied = new(
        "TOPUP.HISTORY_ACCESS_DENIED",
        "The current actor is not permitted to view top-up history.");

    public static readonly Error OrganizationOutsideScope = new(
        "TOPUP.HISTORY_ORGANIZATION_OUTSIDE_SCOPE",
        "The requested organization is outside the current actor's scope.");

    public static readonly Error OrganizationScopeRequired = new(
        "TOPUP.HISTORY_ORGANIZATION_SCOPE_REQUIRED",
        "An organization scope is required to view top-up history.");
}
