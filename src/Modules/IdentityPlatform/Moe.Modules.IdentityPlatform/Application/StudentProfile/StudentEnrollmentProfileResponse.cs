namespace Moe.Modules.IdentityPlatform.Application.StudentProfile;

public sealed record StudentEnrollmentProfileResponse(
    long? SchoolEnrollmentId,
    long? SchoolOrganizationId,
    string? SchoolOrganizationCode,
    string? SchoolOrganizationName,
    string? StudentNumber,
    string? AcademicYear,
    string? LevelCode,
    string? ClassCode,
    string? SchoolingStatusCode,
    DateOnly? StartDate,
    DateOnly? EndDate);
