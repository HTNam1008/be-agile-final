namespace Moe.Modules.IdentityPlatform.Api.Admin;

public sealed record CreateAdminUserRequest(
    string Email,
    string DisplayName,
    string MailNickname,
    string TemporaryPassword,
    long InitialOrganizationUnitId,
    string RoleCode,
    bool AccountEnabled = true);
