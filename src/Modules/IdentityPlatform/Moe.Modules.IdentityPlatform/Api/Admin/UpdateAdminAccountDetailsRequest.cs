namespace Moe.Modules.IdentityPlatform.Api.Admin;

public sealed record UpdateAdminAccountDetailsRequest(
    string? ClassCode,
    string? ResidentialAddress,
    string? Email,
    string? ContactNumber,
    DateTime? ExpectedUpdatedAtUtc);
