using Moe.Application.Abstractions.Messaging;
using Moe.Modules.IdentityPlatform.Application.ExternalProvisioning;

namespace Moe.Modules.IdentityPlatform.Application.ExternalProvisioning.RetryIdentityProvisioning;

public sealed record RetryIdentityProvisioningCommand(long IdentityProvisioningRequestId) : ICommand<IdentityProvisioningRequestResponse>;
