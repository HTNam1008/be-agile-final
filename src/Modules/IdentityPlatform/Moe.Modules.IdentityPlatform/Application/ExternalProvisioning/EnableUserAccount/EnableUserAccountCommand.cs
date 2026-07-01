using Moe.Application.Abstractions.Messaging;
using Moe.Modules.IdentityPlatform.Application.ExternalProvisioning.DisableUserAccount;

namespace Moe.Modules.IdentityPlatform.Application.ExternalProvisioning.EnableUserAccount;

public sealed record EnableUserAccountCommand(long UserAccountId) : ICommand<DisableUserAccountResponse>;
