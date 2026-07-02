using Moe.Application.Abstractions.Clock;
using Moe.Application.Abstractions.Messaging;
using Moe.Application.Abstractions.Security;
using Moe.Modules.IdentityPlatform.Application;
using Moe.Modules.IdentityPlatform.Domain.Iam;
using Moe.Modules.IdentityPlatform.IGateway.Authentication;
using Moe.SharedKernel.Results;

namespace Moe.Modules.IdentityPlatform.Application.Authentication.RecordAdminLogin;

internal sealed class RecordAdminLoginHandler(
    ICurrentUser currentUser,
    IAdminLoginRecorder loginRecorder,
    IClock clock) : ICommandHandler<RecordAdminLoginCommand>
{
    public async Task<Result> Handle(
        RecordAdminLoginCommand command,
        CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated
            || currentUser.UserAccountId is null
            || currentUser.Portal != PortalAccessCodes.Admin)
        {
            return Result.Failure(IdentityErrors.AuthenticatedAdminRequired);
        }

        bool recorded = await loginRecorder.RecordSuccessfulLoginAsync(
            currentUser.UserAccountId.Value,
            clock.UtcNow.UtcDateTime,
            cancellationToken);

        return recorded
            ? Result.Success()
            : Result.Failure(IdentityErrors.UserAccountNotFound);
    }
}
