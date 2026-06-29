namespace Moe.Modules.Mfa.Application.GetMfaStatus;

public sealed record MfaStatusResponse(
    string StatusCode,
    DateTime? LockedUntilUtc,
    DateTime? LastVerifiedAtUtc,
    bool SessionVerified);
