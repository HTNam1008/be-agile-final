namespace Moe.Modules.IdentityPlatform.Api.EService;

public sealed record UpdateContactPreferencesRequest(
    string? PreferredEmail,
    string? PreferredMobile,
    string? PreferredAddress,
    DateTime? ExpectedUpdatedAtUtc);
