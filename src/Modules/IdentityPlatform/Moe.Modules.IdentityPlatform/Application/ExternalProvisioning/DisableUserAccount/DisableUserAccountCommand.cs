using Moe.Application.Abstractions.Messaging;

namespace Moe.Modules.IdentityPlatform.Application.ExternalProvisioning.DisableUserAccount;

public sealed record DisableUserAccountCommand(long UserAccountId) : ICommand<DisableUserAccountResponse>;
