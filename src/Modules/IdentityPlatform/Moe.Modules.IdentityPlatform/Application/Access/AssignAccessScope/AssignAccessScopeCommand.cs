using Moe.Application.Abstractions.Messaging;

namespace Moe.Modules.IdentityPlatform.Application.Access.AssignAccessScope;

public sealed record AssignAccessScopeCommand(
    long UserAccountId,
    long OrganizationUnitId,
    string RoleCode,
    DateTime? EffectiveFromUtc) : ICommand<AssignAccessScopeResponse>;
