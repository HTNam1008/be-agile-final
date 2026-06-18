namespace Moe.Modules.IdentityPlatform.IGateway.Students.TopUpSearch;

public sealed record TopUpStudentSearchCriteria(
    string? Search,
    IReadOnlyCollection<long>? CandidatePersonIds,
    IReadOnlyCollection<long>? AccountSearchPersonIds,
    long? OrganizationId,
    string? SchoolingStatusCode,
    string? LevelCode,
    string? ClassCode,
    int? AgeFrom,
    int? AgeTo,
    int Page,
    int PageSize);
