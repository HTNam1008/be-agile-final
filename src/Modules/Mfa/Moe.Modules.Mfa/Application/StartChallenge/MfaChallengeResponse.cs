namespace Moe.Modules.Mfa.Application.StartChallenge;

public sealed record MfaChallengeResponse(
    Guid ChallengeId,
    DateTime ExpiresAtUtc);
