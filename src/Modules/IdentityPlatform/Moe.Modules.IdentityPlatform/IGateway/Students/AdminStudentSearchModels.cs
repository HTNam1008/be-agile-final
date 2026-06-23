namespace Moe.Modules.IdentityPlatform.IGateway.Students;

public sealed record AdminStudentSearchCriteria(
    long OrganizationId,
    string? Search,
    string? LevelCode,
    string? ClassCode,
    int Page,
    int PageSize);

public sealed record AdminStudentSearchSummary(
    long PersonId,
    string StudentNumber,
    string FullName,
    DateOnly DateOfBirth,
    string LevelCode,
    string ClassCode,
    string SchoolingStatusCode,
    long OrganizationId);
