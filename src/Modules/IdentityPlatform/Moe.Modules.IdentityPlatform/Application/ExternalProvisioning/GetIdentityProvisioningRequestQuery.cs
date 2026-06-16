using Moe.Application.Abstractions.Messaging;

namespace Moe.Modules.IdentityPlatform.Application.ExternalProvisioning;

public sealed record GetIdentityProvisioningRequestQuery(long IdentityProvisioningRequestId) : IQuery<IdentityProvisioningRequestResponse>;
