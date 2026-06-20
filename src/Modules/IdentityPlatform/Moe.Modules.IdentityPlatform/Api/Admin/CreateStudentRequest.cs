namespace Moe.Modules.IdentityPlatform.Api.Admin;

public sealed record CreateStudentRequest(
    string? SchoolName,
    string IdentityNumber,
    string FullName,
    DateOnly DateOfBirth,
    string NationalityCode,
    string CitizenshipStatusCode,
    string StudentNumber,
    string AcademicYear,
    string LevelCode,
    string ClassCode,
    DateOnly? StartDate,
    string? Email,
    string? Mobile,
    string? Address,
    bool IsAccountHolder = true);
