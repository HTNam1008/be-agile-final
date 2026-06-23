using Moe.Application.Abstractions.Clock;
using Moe.Application.Abstractions.Messaging;
using Moe.Modules.IdentityPlatform.Application;
using Moe.Modules.IdentityPlatform.Domain.Iam;
using Moe.Modules.IdentityPlatform.IGateway.Repositories;
using Moe.SharedKernel.Results;

namespace Moe.Modules.IdentityPlatform.Application.ExternalProvisioning.DisableUserAccount;

internal sealed class DisableUserAccountHandler(
    IUserAccountRepository userAccounts,
    IClock clock) : ICommandHandler<DisableUserAccountCommand, DisableUserAccountResponse>
{
    public async Task<Result<DisableUserAccountResponse>> Handle(DisableUserAccountCommand command, CancellationToken cancellationToken)
    {
        UserAccount? account = await userAccounts.DisableAsync(
            command.UserAccountId,
            clock.UtcNow.UtcDateTime,
            cancellationToken);

        if (account is null)
        {
            return Result<DisableUserAccountResponse>.Failure(IdentityErrors.UserAccountNotFound);
        }

        return Result<DisableUserAccountResponse>.Success(new DisableUserAccountResponse(account.Id, account.AccountStatusCode));
    }
}
