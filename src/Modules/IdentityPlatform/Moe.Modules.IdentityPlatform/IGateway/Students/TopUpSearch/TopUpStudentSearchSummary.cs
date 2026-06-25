namespace Moe.Modules.IdentityPlatform.IGateway.Students.TopUpSearch;

public sealed record TopUpStudentSearchSummary(
    long PersonId,
    string StudentNumber,
    string DisplayName,
    DateOnly DateOfBirth,
    string SchoolingStatusCode,
    string LevelCode,
    string? ClassCode,
    long OrganizationId);
