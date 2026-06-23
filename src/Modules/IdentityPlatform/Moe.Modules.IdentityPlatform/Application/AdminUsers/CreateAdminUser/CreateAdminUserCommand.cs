using Moe.Application.Abstractions.Messaging;

namespace Moe.Modules.IdentityPlatform.Application.AdminUsers.CreateAdminUser;

public sealed record CreateAdminUserCommand(
    string Email,
    string DisplayName,
    string MailNickname,
    string TemporaryPassword,
    long InitialOrganizationUnitId,
    string RoleCode,
    bool AccountEnabled = true) : ICommand<CreateAdminUserResponse>;
