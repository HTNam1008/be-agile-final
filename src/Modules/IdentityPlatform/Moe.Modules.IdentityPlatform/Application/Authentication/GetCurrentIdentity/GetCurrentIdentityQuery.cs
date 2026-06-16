using Moe.Application.Abstractions.Messaging;
using Moe.Modules.IdentityPlatform.IGateway.Authentication;

namespace Moe.Modules.IdentityPlatform.Application.Authentication.GetCurrentIdentity;

public sealed record GetCurrentIdentityQuery : IQuery<LocalIdentitySummary>;
