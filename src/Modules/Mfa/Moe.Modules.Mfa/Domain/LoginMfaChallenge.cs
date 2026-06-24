using Moe.SharedKernel.Domain;

namespace Moe.Modules.Mfa.Domain;

internal sealed class LoginMfaChallenge : Entity<Guid>
{
    private LoginMfaChallenge() : base(Guid.Empty) { }

    private LoginMfaChallenge(
        Guid id,
        long loginAccountId,
        string purposeCode,
        DateTime expiresAtUtc,
        DateTime utcNow) : base(id)
    {
        LoginAccountId = loginAccountId;
        PurposeCode = purposeCode;
        StatusCode = MfaChallengeStatusCodes.Pending;
        ExpiresAtUtc = expiresAtUtc;
        CreatedAtUtc = utcNow;
    }

    public long LoginAccountId { get; private set; }
    public string PurposeCode { get; private set; } = string.Empty;
    public string StatusCode { get; private set; } = string.Empty;
    public int FailedAttemptCount { get; private set; }
    public DateTime ExpiresAtUtc { get; private set; }
    public DateTime? VerifiedAtUtc { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    public bool IsExpired(DateTime utcNow)
    {
        return ExpiresAtUtc <= utcNow;
    }

    public static LoginMfaChallenge Create(
        long loginAccountId,
        string purposeCode,
        TimeSpan lifetime,
        DateTime utcNow)
    {
        return new LoginMfaChallenge(
            Guid.NewGuid(),
            loginAccountId,
            purposeCode,
            utcNow.Add(lifetime),
            utcNow);
    }

    public void MarkVerified(DateTime utcNow)
    {
        StatusCode = MfaChallengeStatusCodes.Verified;
        VerifiedAtUtc = utcNow;
    }

    public void MarkExpired()
    {
        StatusCode = MfaChallengeStatusCodes.Expired;
    }

    public void MarkFailed()
    {
        FailedAttemptCount++;
        StatusCode = MfaChallengeStatusCodes.Failed;
    }
}
