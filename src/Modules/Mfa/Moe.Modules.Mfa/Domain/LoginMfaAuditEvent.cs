using Moe.SharedKernel.Domain;

namespace Moe.Modules.Mfa.Domain;

internal sealed class LoginMfaAuditEvent : Entity<long>
{
    private LoginMfaAuditEvent() : base(0) { }

    public LoginMfaAuditEvent(
        long loginAccountId,
        Guid? loginMfaChallengeId,
        string eventCode,
        long? performedByAccountId,
        string? reason,
        string? ipAddress,
        string? userAgent,
        DateTime utcNow) : base(0)
    {
        LoginAccountId = loginAccountId;
        LoginMfaChallengeId = loginMfaChallengeId;
        EventCode = eventCode;
        PerformedByAccountId = performedByAccountId;
        Reason = reason;
        IpAddress = ipAddress;
        UserAgent = userAgent;
        CreatedAtUtc = utcNow;
    }

    public long LoginAccountId { get; private set; }
    public Guid? LoginMfaChallengeId { get; private set; }
    public string EventCode { get; private set; } = string.Empty;
    public long? PerformedByAccountId { get; private set; }
    public string? Reason { get; private set; }
    public string? IpAddress { get; private set; }
    public string? UserAgent { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
}
