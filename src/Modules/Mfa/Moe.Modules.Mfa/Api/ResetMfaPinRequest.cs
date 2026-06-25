namespace Moe.Modules.Mfa.Api;

public sealed record ResetMfaPinRequest(long LoginAccountId, string Reason);
