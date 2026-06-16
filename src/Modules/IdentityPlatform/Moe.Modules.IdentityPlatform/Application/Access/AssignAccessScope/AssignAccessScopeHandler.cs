using Moe.Application.Abstractions.Clock;
using Moe.Application.Abstractions.Messaging;
using Moe.Application.Abstractions.Security;
using Moe.Modules.IdentityPlatform.Application;
using Moe.Modules.IdentityPlatform.Domain.Iam;
using Moe.Modules.IdentityPlatform.IGateway.Repositories;
using Moe.SharedKernel.Results;

namespace Moe.Modules.IdentityPlatform.Application.Access.AssignAccessScope;

internal sealed class AssignAccessScopeHandler(
    IAccessScopeRepository accessScopes,
    ICurrentUser currentUser,
    IClock clock) : ICommandHandler<AssignAccessScopeCommand, AssignAccessScopeResponse>
{
    public async Task<Result<AssignAccessScopeResponse>> Handle(AssignAccessScopeCommand command, CancellationToken cancellationToken)
    {
        if (currentUser.UserAccountId is null)
        {
            return Result<AssignAccessScopeResponse>.Failure(IdentityErrors.AuthenticatedAdminRequired);
        }

        DateTime utcNow = clock.UtcNow.UtcDateTime;
        string normalizedRoleCode = command.RoleCode.Trim().ToUpperInvariant();

        if (!await accessScopes.UserAccountCanReceiveAccessAsync(command.UserAccountId, cancellationToken))
        {
            return Result<AssignAccessScopeResponse>.Failure(IdentityErrors.UserAccountNotFound);
        }

        if (!await accessScopes.IsActiveOrganizationUnitAsync(command.OrganizationUnitId, utcNow, cancellationToken))
        {
            return Result<AssignAccessScopeResponse>.Failure(IdentityErrors.OrganizationUnitNotFound);
        }

        if (!await accessScopes.HasActiveRolePermissionsAsync(normalizedRoleCode, utcNow, cancellationToken))
        {
            return Result<AssignAccessScopeResponse>.Failure(IdentityErrors.RoleNotConfigured);
        }

        if (await accessScopes.HasActiveScopeAsync(
            command.UserAccountId,
            command.OrganizationUnitId,
            normalizedRoleCode,
            utcNow,
            cancellationToken))
        {
            return Result<AssignAccessScopeResponse>.Failure(IdentityErrors.ActiveAccessScopeAlreadyExists);
        }

        UserAccessScope scope = new(
            command.UserAccountId,
            command.OrganizationUnitId,
            normalizedRoleCode,
            currentUser.UserAccountId.Value,
            command.EffectiveFromUtc ?? utcNow,
            utcNow);

        await accessScopes.AddAsync(scope, cancellationToken);

        return Result<AssignAccessScopeResponse>.Success(new AssignAccessScopeResponse(scope.Id, scope.StatusCode));
    }
}
