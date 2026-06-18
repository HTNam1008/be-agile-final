namespace Moe.Modules.IdentityPlatform.Api.Admin;

public sealed record UpdateMyAdminContactRequest(
    string? ContactEmail,
    string? ContactMobile);
