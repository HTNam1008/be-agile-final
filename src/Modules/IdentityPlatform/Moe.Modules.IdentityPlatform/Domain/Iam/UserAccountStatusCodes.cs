namespace Moe.Modules.IdentityPlatform.Domain.Iam;

internal static class UserAccountStatusCodes
{
    public const string PendingFirstLogin = "PENDING_FIRST_LOGIN";
    public const string Active = "ACTIVE";
    public const string Disabled = "DISABLED";
    public const string Locked = "LOCKED";
    public const string Revoked = "REVOKED";
}
