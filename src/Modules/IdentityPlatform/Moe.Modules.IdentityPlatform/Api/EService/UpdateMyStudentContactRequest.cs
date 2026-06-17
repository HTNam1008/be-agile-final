namespace Moe.Modules.IdentityPlatform.Api.EService;

public sealed record UpdateMyStudentContactRequest(
    string? ContactEmail,
    string? ContactMobile);
