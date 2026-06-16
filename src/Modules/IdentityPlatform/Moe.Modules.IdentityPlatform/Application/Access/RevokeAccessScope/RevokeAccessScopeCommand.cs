using Moe.Application.Abstractions.Messaging;

namespace Moe.Modules.IdentityPlatform.Application.Access.RevokeAccessScope;

public sealed record RevokeAccessScopeCommand(long UserAccessScopeId) : ICommand<RevokeAccessScopeResponse>;
