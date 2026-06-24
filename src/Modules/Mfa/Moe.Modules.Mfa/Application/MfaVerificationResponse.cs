namespace Moe.Modules.Mfa.Application;

public sealed record MfaVerificationResponse(
    bool Verified,
    long LoginAccountId,
    string PurposeCode,
    DateTime? VerifiedAtUtc);
