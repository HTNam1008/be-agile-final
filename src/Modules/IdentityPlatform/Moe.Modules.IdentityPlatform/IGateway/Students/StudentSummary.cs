namespace Moe.Modules.IdentityPlatform.IGateway.Students;

public sealed record StudentSummary(
    long PersonId,
    string DisplayName,
    DateOnly DateOfBirth,
    bool IsAccountHolder,
    string? SchoolName);
