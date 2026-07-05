namespace Moe.Modules.Mfa.Domain;

internal static class MfaTypeCodes
{
    public const string Pin = "PIN";
}

internal static class MfaCredentialStatusCodes
{
    public const string Active = "ACTIVE";
    public const string Locked = "LOCKED";
    public const string Disabled = "DISABLED";
    public const string ResetRequired = "RESET_REQUIRED";
}

internal static class MfaChallengePurposeCodes
{
    public const string Login = "LOGIN";
    public const string Setup = "SETUP";
    public const string Verify = "VERIFY";
    public const string Recovery = "RECOVERY";
}

internal static class MfaChallengeStatusCodes
{
    public const string Pending = "PENDING";
    public const string Verified = "VERIFIED";
    public const string Expired = "EXPIRED";
    public const string Failed = "FAILED";
}

internal static class MfaAuditEventCodes
{
    public const string ChallengeStarted = "MFA_CHALLENGE_STARTED";
    public const string PinSet = "MFA_PIN_SET";
    public const string VerifySuccess = "MFA_VERIFY_SUCCESS";
    public const string VerifyFailed = "MFA_VERIFY_FAILED";
    public const string PinChanged = "MFA_PIN_CHANGED";
    public const string PinResetRequired = "MFA_PIN_RESET_REQUIRED";
    public const string Locked = "MFA_LOCKED";
    public const string RecoveryRequested = "MFA_RECOVERY_REQUESTED";
    public const string RecoveryCompleted = "MFA_RECOVERY_COMPLETED";
}
