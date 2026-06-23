using Moe.Application.Abstractions.Clock;
using Moe.Application.Abstractions.Messaging;
using Moe.Modules.IdentityPlatform.Application;
using Moe.Modules.IdentityPlatform.Domain.Iam;
using Moe.Modules.IdentityPlatform.IGateway.Repositories;
using Moe.SharedKernel.Results;

namespace Moe.Modules.IdentityPlatform.Application.Access.RevokeAccessScope;

internal sealed class RevokeAccessScopeHandler(
    IAccessScopeRepository accessScopes,
    IClock clock) : ICommandHandler<RevokeAccessScopeCommand, RevokeAccessScopeResponse>
{
    public async Task<Result<RevokeAccessScopeResponse>> Handle(RevokeAccessScopeCommand command, CancellationToken cancellationToken)
    {
        UserAccessScope? scope = await accessScopes.RevokeAsync(
            command.UserAccessScopeId,
            clock.UtcNow.UtcDateTime,
            cancellationToken);

        if (scope is null)
        {
            return Result<RevokeAccessScopeResponse>.Failure(IdentityErrors.UserAccountNotFound);
        }

        return Result<RevokeAccessScopeResponse>.Success(new RevokeAccessScopeResponse(scope.Id, scope.StatusCode));
    }
}
