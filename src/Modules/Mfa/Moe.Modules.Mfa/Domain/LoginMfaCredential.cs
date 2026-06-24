using Moe.SharedKernel.Domain;

namespace Moe.Modules.Mfa.Domain;

internal sealed class LoginMfaCredential : Entity<long>
{
    private LoginMfaCredential() : base(0) { }

    private LoginMfaCredential(
        long loginAccountId,
        string mfaTypeCode,
        byte[] secretHash,
        byte[] secretSalt,
        string secretHashAlgorithm,
        DateTime utcNow) : base(0)
    {
        LoginAccountId = loginAccountId;
        MfaTypeCode = mfaTypeCode;
        SecretHash = secretHash;
        SecretSalt = secretSalt;
        SecretHashAlgorithm = secretHashAlgorithm;
        StatusCode = MfaCredentialStatusCodes.Active;
        CreatedAtUtc = utcNow;
        UpdatedAtUtc = utcNow;
    }

    public long LoginAccountId { get; private set; }
    public string MfaTypeCode { get; private set; } = string.Empty;
    public byte[] SecretHash { get; private set; } = [];
    public byte[] SecretSalt { get; private set; } = [];
    public string SecretHashAlgorithm { get; private set; } = string.Empty;
    public string StatusCode { get; private set; } = string.Empty;
    public int FailedAttemptCount { get; private set; }
    public DateTime? LockedUntilUtc { get; private set; }
    public DateTime? LastVerifiedAtUtc { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public bool IsLocked(DateTime utcNow)
    {
        return LockedUntilUtc.HasValue && LockedUntilUtc.Value > utcNow;
    }

    public static LoginMfaCredential CreatePin(
        long loginAccountId,
        byte[] secretHash,
        byte[] secretSalt,
        string secretHashAlgorithm,
        DateTime utcNow)
    {
        return new LoginMfaCredential(
            loginAccountId,
            MfaTypeCodes.Pin,
            secretHash,
            secretSalt,
            secretHashAlgorithm,
            utcNow);
    }

    public void ReplaceSecret(byte[] secretHash, byte[] secretSalt, string secretHashAlgorithm, DateTime utcNow)
    {
        SecretHash = secretHash;
        SecretSalt = secretSalt;
        SecretHashAlgorithm = secretHashAlgorithm;
        FailedAttemptCount = 0;
        LockedUntilUtc = null;
        StatusCode = MfaCredentialStatusCodes.Active;
        UpdatedAtUtc = utcNow;
    }

    public void RequireReset(DateTime utcNow)
    {
        FailedAttemptCount = 0;
        LockedUntilUtc = null;
        StatusCode = MfaCredentialStatusCodes.ResetRequired;
        UpdatedAtUtc = utcNow;
    }

    public void RecordVerified(DateTime utcNow)
    {
        FailedAttemptCount = 0;
        LockedUntilUtc = null;
        LastVerifiedAtUtc = utcNow;
        UpdatedAtUtc = utcNow;
    }

    public bool RecordFailedAttempt(int maxFailedAttempts, TimeSpan lockoutDuration, DateTime utcNow)
    {
        FailedAttemptCount++;
        UpdatedAtUtc = utcNow;

        if (FailedAttemptCount < maxFailedAttempts)
        {
            return false;
        }

        LockedUntilUtc = utcNow.Add(lockoutDuration);
        return true;
    }
}
