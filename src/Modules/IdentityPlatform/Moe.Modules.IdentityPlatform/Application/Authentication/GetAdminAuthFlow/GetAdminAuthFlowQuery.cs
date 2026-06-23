using Moe.Application.Abstractions.Messaging;

namespace Moe.Modules.IdentityPlatform.Application.Authentication.GetAdminAuthFlow;

public sealed record GetAdminAuthFlowQuery : IQuery<AdminAuthFlowResponse>;
