namespace Moe.Modules.Mfa.Api;

public sealed record VerifyMfaPinRequest(Guid ChallengeId, string Pin);
