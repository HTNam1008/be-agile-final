using Moe.Application.Abstractions.Messaging;

namespace Moe.Modules.IdentityPlatform.Application.AdminProfile.UpdateMyAdminContact;

public sealed record UpdateMyAdminContactCommand(
    string? ContactEmail,
    string? ContactMobile) : ICommand<AdminProfileResponse>;
