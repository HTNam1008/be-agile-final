namespace Moe.Modules.IdentityPlatform.IGateway.Students;

public sealed record EServiceEligibilitySummary(
    long PersonId,
    bool IsEligible,
    string? ReasonCode);
